using UnityEngine;

namespace Pootle
{
    /// <summary>A parked bus unfolds a walking route to its roof; driving stows the access stairs.</summary>
    [DefaultExecutionOrder(-10), DisallowMultipleComponent, RequireComponent(typeof(PootleCar))]
    public sealed class PootleBusPlatform : MonoBehaviour
    {
        public int fleetNumber;
        public GameObject accessStairs;
        public Transform roofPoint;
        public Transform[] stairWaypoints;
        public Transform destinationLanding;
        public bool StairsDeployed => accessStairs && accessStairs.activeSelf;
        PootleCar car;

        void Awake() => car = GetComponent<PootleCar>();
        void OnEnable() { if (!car) car = GetComponent<PootleCar>(); RefreshAccess(); }
        void Update() => RefreshAccess();
        void RefreshAccess()
        {
            bool deploy = car && !car.IsOccupied && car.IsParked;
            if (accessStairs && accessStairs.activeSelf != deploy) accessStairs.SetActive(deploy);
        }
    }
}
