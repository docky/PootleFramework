using UnityEngine;

namespace Pootle
{
    /// <summary>Optional action/air-steering policy. The character still owns velocity and motor movement.</summary>
    public interface IPootleMovementPolicy
    {
        // Called after support resolution, before integration. NaN means no vertical impulse.
        float BeforeMovement(PootleCharacter character, float dt, PootleInputFrame input, bool groundedAtTickStart);
        Vector3 AirVelocity(Vector3 current, Vector3 direction, float magnitude, float dt);
    }
}
