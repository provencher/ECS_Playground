
using Unity.Mathematics;

namespace Utils
{
    public static class UnityMathUtils
    {
        /// <summary>
        /// Compares two floating point values and returns true if they are similar.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="epsilon"></param>
        public static bool Approximately(float a, float b, float epsilon)
        {
            return math.abs(b - a) < math.max(1E-06f * math.max(math.abs(a), math.abs(b)), epsilon * 8f);
        }
    }
}
