RWTexture3D<half> deposit_in: register(u0);
RWTexture3D<half> deposit_out: register(u1);
RWTexture3D<half4> tex_trace: register(u2);

cbuffer ConfigBuffer : register(b0)
{
    float decay_factor;
    int grid_width;
    int grid_height;
    int grid_depth;
};

[numthreads(8,8,8)]
void main(uint3 threadIDInGroup : SV_GroupThreadID, uint3 groupID : SV_GroupID, uint3 dispatchThreadId : SV_DispatchThreadID){
    uint3 p = dispatchThreadId.xyz;

    // Average deposit values in a 3x3x3 neighborhood
    // Apply distance-based weighting to prevent overestimation along diagonals
    float v = 0.0;
    float w = 0.0;
    for (int dx = -1; dx <= 1; dx++) {
        for (int dy = -1; dy <= 1; dy++) {
            for (int dz = -1; dz <= 1; dz++) {
                float weight = (all(int3(dx,dy,dz)) == 0)? 1.0 : 1.0 / sqrt(float(abs(dx) + abs(dy) + abs(dz)));
                int3 txcoord = int3(p) + int3(dx, dy, dz);
                txcoord.x = txcoord.x % grid_width;
                txcoord.y = txcoord.y % grid_height;
                txcoord.z = txcoord.z % grid_depth;
                v += weight * deposit_in[txcoord];
                w += weight;
            }
        }
    }
    v /= w;

    // Decay the deposit by a constant factor
    v *= decay_factor;
    deposit_out[p] = v;

    // Decay the trace a little
    tex_trace[p] *= 0.995;
}

