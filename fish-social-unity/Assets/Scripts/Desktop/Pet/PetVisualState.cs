namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Own-cat presentation states. Network/window code must not depend on renderer type.
    /// Wire values match ToWire(): idle, sit, cast, fishing, hooked, reel, dragging, offline.
    /// </summary>
    public enum PetVisualState
    {
        Idle = 0,
        Fishing = 1,
        Hooked = 2,
        Catching = 3,
        Dragging = 4,
        Offline = 5,
        Sit = 6,
        Cast = 7,
        Reel = 8,
    }
}
