using Float = Deterministic.GameFramework.Types.Float;
namespace Deterministic.GameFramework.Box2D
{
    public enum B2UserDataType : byte // must be byte!!
    {
        None = 0,
        Signed = 1,
        Unsigned = 2,
        Double = 3,
        Ref = 4,
    }
}