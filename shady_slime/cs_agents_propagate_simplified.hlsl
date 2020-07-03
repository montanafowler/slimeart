RWTexture3D<half> tex_deposit: register(u0);
RWTexture3D<half4> tex_trace: register(u1);

RWStructuredBuffer<float> particles_x: register(u2);
RWStructuredBuffer<float> particles_y: register(u3);
RWStructuredBuffer<float> particles_z: register(u4);
RWStructuredBuffer<float> particles_phi: register(u5);
RWStructuredBuffer<float> particles_theta: register(u6);
RWStructuredBuffer<float> particles_weights: register(u7);

cbuffer ConfigBuffer : register(b0)
{
    float sense_spread; // default = 15-30 degrees
    float sense_distance; // in world-space units; default = about 1/100 of the world 'cube' size
    float turn_angle; // default = 15 degrees
    float move_distance; // in world-space units; default = about 1/5--1/3 of sense_distance
    float deposit_value; // should be called agent deposit // 0 for data-driven fitting, >0 for self-reinforcing behavior
    int world_width; //
    int world_height; // grid dimensions - note that the particle positions are also stored in the grid coordinates, but continuous
    int world_depth; //
    float move_sense_coef;
    float normalization_factor;
};

#define PI 3.141592
#define HALFPI 0.5 * PI
#define TWOPI 2.0 * PI

struct RNG {
    #define BAD_W 0x464fffffU
    #define BAD_Z 0x9068ffffU
    uint m_w;
	uint m_z;

    void set_seed(uint seed1, uint seed2)
	{
		m_w = seed1;
		m_z = seed2;
		if (m_w == 0U || m_w == BAD_W) ++m_w;
		if (m_w == 0U || m_z == BAD_Z) ++m_z;
	}

    void get_seed(out uint seed1, out uint seed2)
    {
        seed1 = m_w;
        seed2 = m_z;
    }

    uint random_uint()
	{
		m_z = 36969U * (m_z & 65535U) + (m_z >> 16U);
		m_w = 18000U * (m_w & 65535U) + (m_w >> 16U);
		return uint((m_z << 16U) + m_w);
	}

    float random_float()
	{
		return float(random_uint()) / float(0xFFFFFFFFU);
	}
};

uint wang_hash(uint seed)
{
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
    return seed;
}

float3 rotate(float3 v, float3 a, float angle) {
    float3 result = cos(angle) * v + sin(angle) * (cross(a, v)) + dot(a, v) * (1.0 - cos(angle)) * a;
    return result;
}

float mod(float x, float y) {
    return x - y * floor(x / y);
}

[numthreads(10,10,10)]
void main(uint thread_index : SV_GroupIndex, uint3 group_id : SV_GroupID) {
    uint group_idx = group_id.x + group_id.y * 10 + group_id.z * 100;
    uint idx = thread_index + 1000 * group_idx;

    // Fetch current particle state
    float x = particles_x[idx];
    float y = particles_y[idx];
    float z = particles_z[idx];
    float th = particles_theta[idx];
    float ph = particles_phi[idx];
    float particle_weight = particles_weights[idx];
    bool is_data = (th < -1.0); // FLAG IF TRUE PUT THE GALIXY IN THE DEPOSIT // RANDOMLY WRITE DOTS SO I ESTABLISH THE COMMUNICATION BETWEEN THE TWO SHADERS

    // If data, then just write out deposit and exit
    if (is_data) {
        tex_deposit[uint3(x, y, z)] += 10.0 * particle_weight; // NOT ATOMIC!
        return;
    }

    RNG rng;
    rng.set_seed(
        wang_hash(359*idx),
        wang_hash(uint(x*y*z))
    );

    // Get vector which points in the current particle's direction
    float3 center_axis = float3(sin(th) * cos(ph), cos(th), sin(th) * sin(ph));

    // Get base vector which points away from the current particle's direction and will be used to sample environment in other directions
    float sense_theta = th - sense_spread;
    float3 off_center_base_dir = float3(sin(sense_theta) * cos(ph), cos(sense_theta), sin(sense_theta) * sin(ph));

    // Probabilistic sensing
    float sense_distance_prob = sense_distance;
    float xi = clamp(rng.random_float(), 0.001, 0.999); // random for each particle and timestep
    float distance_scaling_factor = -0.3033 * log( (pow(xi+0.005, -0.4) - 0.9974) / 7.326 ); // using Maxwell-Boltzmann distribution
    sense_distance_prob *= distance_scaling_factor;

    // Sample environment along the movement axis
    int3 p = int3(x, y, z);
    float current_deposit = tex_deposit[p];
    float3 center_sense_pos = center_axis * sense_distance_prob;
    float deposit_ahead = tex_deposit[p + int3(center_sense_pos)];

    // Stochastic MC direction sampling
    float random_angle = rng.random_float() * TWOPI - PI;
    float3 sense_offset = rotate(off_center_base_dir, center_axis, random_angle) * sense_distance_prob;
    float sense_deposit = tex_deposit[p + int3(sense_offset)];

    float sharpness = move_sense_coef;
    float p_straight = pow(max(deposit_ahead, 0.0), sharpness);
    float p_turn = pow(max(sense_deposit, 0.0), sharpness);
    float xiDir = rng.random_float();
    if (p_straight + p_turn > 1.0e-5)
        if (p_turn > p_straight) {
            float theta_turn = th - turn_angle;
            float3 off_center_base_dir_turn = float3(sin(theta_turn) * cos(ph), cos(theta_turn), sin(theta_turn) * sin(ph));
            float3 new_direction = rotate(off_center_base_dir_turn, center_axis, random_angle);
            ph = atan2(new_direction.z, new_direction.x);
            th = acos(new_direction.y / length(new_direction));
        }

    // Make a step
    float3 dp = float3(sin(th) * cos(ph), cos(th), sin(th) * sin(ph)) * move_distance * (0.1 + 0.9 * distance_scaling_factor);
    x += dp.x;
    y += dp.y;
    z += dp.z;

    // Keep the particle inside environment
    x = mod(x, world_width);
    y = mod(y, world_height);
    z = mod(z, world_depth);

    // Update particle state
    particles_x[idx] = x;
    particles_y[idx] = y;
    particles_z[idx] = z;
    particles_theta[idx] = th;
    particles_phi[idx] = ph;
    particles_weights[idx] = particle_weight;

    // Update deposit and trace grids
    tex_deposit[uint3(x, y, z)] += deposit_value; // NOT ATOMIC! // the trace is not blurred, trace is not food, but agents also leave deposit
    tex_trace[uint3(x, y, z)] += float4(distance_scaling_factor/*actualtrace*/, abs(center_axis.x), abs(center_axis.y), abs(center_axis.z)/*direction of agents*/); // NOT ATOMIC!
}
