using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetStudio
{
    public static class BuildTypes
    {
        public static readonly string
            Alpha = "a",
            Beta = "b",
            Final = "f",
            Patch = "p",
            Tuanjie = "t";
    }

    public class UnityVersion : IComparable
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int Build { get; }
        public string BuildType { get; }
        public string FullVersion { get; }

        public bool IsStripped => this == (0, 0, 0);
        public bool IsAlpha => BuildType == BuildTypes.Alpha;
        public bool IsBeta => BuildType == BuildTypes.Beta;
        public bool IsPatch => BuildType == BuildTypes.Patch;
        public bool IsTuanjie => BuildType == BuildTypes.Tuanjie;

        public UnityVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                throw new ArgumentException("Unity version cannot be empty.");

            try
            {
                int[] ver = Regex.Matches(version, @"\d+").Cast<Match>().Select(x => int.Parse(x.Value)).ToArray();
                (Major, Minor, Patch) = (ver[0], ver[1], ver[2]);
                if (ver.Length >= 4)
                    Build = ver[3];
                FullVersion = version;
            }
            catch (Exception)
            {
                throw new NotSupportedException($"Failed to parse Unity version: \"{version}\".");
            }

            string[] build = Regex.Matches(version, @"\D+").Cast<Match>().Select(x => x.Value).ToArray();
            if (build.Length > 2)
            {
                BuildType = build[2];
            }
        }

        public UnityVersion(int major = 0, int minor = 0, int patch = 0)
        {
            (Major, Minor, Patch) = (major, minor, patch);
            FullVersion = $"{Major}.{Minor}.{Patch}";
            if (!IsStripped)
            {
                Build = 1;
                BuildType = BuildTypes.Final;
                FullVersion += $"{BuildType}{Build}";
            }
        }

        #region UnityVer, UnityVer
        public static bool operator ==(UnityVersion left, UnityVersion right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(UnityVersion left, UnityVersion right)
        {
            return !Equals(left, right);
        }

        public static bool operator >(UnityVersion left, UnityVersion right)
        {
            return left?.CompareTo(right) > 0;
        }

        public static bool operator <(UnityVersion left, UnityVersion right)
        {
            return left?.CompareTo(right) < 0;
        }

        public static bool operator >=(UnityVersion left, UnityVersion right)
        {
            return left == right || left > right;
        }

        public static bool operator <=(UnityVersion left, UnityVersion right)
        {
            return left == right || left < right;
        }
        #endregion

        #region UnityVer, int
        public static bool operator ==(UnityVersion left, int right)
        {
            return left?.Major == right;
        }

        public static bool operator !=(UnityVersion left, int right)
        {
            return left?.Major != right;
        }

        public static bool operator >(UnityVersion left, int right)
        {
            return left?.Major > right;
        }

        public static bool operator <(UnityVersion left, int right)
        {
            return left?.Major < right;
        }

        public static bool operator >=(UnityVersion left, int right)
        {
            return left?.Major >= right;
        }

        public static bool operator <=(UnityVersion left, int right)
        {
            return left?.Major <= right;
        }
        #endregion

        #region UnityVer, (int, int)
        public static bool operator ==(UnityVersion left, (int, int) right)
        {
            return (left?.Major, left?.Minor) == (right.Item1, right.Item2);
        }

        public static bool operator !=(UnityVersion left, (int, int) right)
        {
            return (left?.Major, left?.Minor) != (right.Item1, right.Item2);
        }

        public static bool operator >(UnityVersion left, (int, int) right)
        {
            return left?.CompareTo(right) > 0;
        }

        public static bool operator <(UnityVersion left, (int, int) right)
        {
            return left?.CompareTo(right) < 0;
        }

        public static bool operator >=(UnityVersion left, (int, int) right)
        {
            return left == right || left > right;
        }

        public static bool operator <=(UnityVersion left, (int, int) right)
        {
            return left == right || left < right;
        }
        #endregion

        #region UnityVer, (int, int, int)
        public static bool operator ==(UnityVersion left, (int, int, int) right)
        {
            return (left?.Major, left?.Minor, left?.Patch) == (right.Item1, right.Item2, right.Item3);
        }

        public static bool operator !=(UnityVersion left, (int, int, int) right)
        {
            return (left?.Major, left?.Minor, left?.Patch) != (right.Item1, right.Item2, right.Item3);
        }

        public static bool operator >(UnityVersion left, (int, int, int) right)
        {
            return left?.CompareTo(right) > 0;
        }

        public static bool operator <(UnityVersion left, (int, int, int) right)
        {
            return left?.CompareTo(right) < 0;
        }

        public static bool operator >=(UnityVersion left, (int, int, int) right)
        {
            return left == right || left > right;
        }

        public static bool operator <=(UnityVersion left, (int, int, int) right)
        {
            return left == right || left < right;
        }
        #endregion

        #region int, UnityVer
        public static bool operator ==(int left, UnityVersion right)
        {
            return left == right?.Major;
        }

        public static bool operator !=(int left, UnityVersion right)
        {
            return left != right?.Major;
        }

        public static bool operator >(int left, UnityVersion right)
        {
            return left > right?.Major;
        }

        public static bool operator <(int left, UnityVersion right)
        {
            return left < right?.Major;
        }

        public static bool operator >=(int left, UnityVersion right)
        {
            return left >= right?.Major;
        }

        public static bool operator <=(int left, UnityVersion right)
        {
            return left <= right?.Major;
        }
        #endregion

        #region (int, int), UnityVer
        public static bool operator ==((int, int) left, UnityVersion right)
        {
            return (left.Item1, left.Item2) == (right?.Major, right?.Minor);
        }

        public static bool operator !=((int, int) left, UnityVersion right)
        {
            return (left.Item1, left.Item2) != (right?.Major, right?.Minor);
        }

        public static bool operator >((int, int) left, UnityVersion right)
        {
            return right?.CompareTo(left) < 0;
        }

        public static bool operator <((int, int) left, UnityVersion right)
        {
            return right?.CompareTo(left) > 0;
        }

        public static bool operator >=((int, int) left, UnityVersion right)
        {
            return left == right || left > right;
        }

        public static bool operator <=((int, int) left, UnityVersion right)
        {
            return left == right || left < right;
        }
        #endregion

        #region (int, int, int), UnityVer
        public static bool operator ==((int, int, int) left, UnityVersion right)
        {
            return (left.Item1, left.Item2, left.Item3) == (right?.Major, right?.Minor, right?.Patch);
        }

        public static bool operator !=((int, int, int) left, UnityVersion right)
        {
            return (left.Item1, left.Item2, left.Item3) != (right?.Major, right?.Minor, right?.Patch);
        }

        public static bool operator >((int, int, int) left, UnityVersion right)
        {
            return right?.CompareTo(left) < 0;
        }

        public static bool operator <((int, int, int) left, UnityVersion right)
        {
            return right?.CompareTo(left) > 0;
        }

        public static bool operator >=((int, int, int) left, UnityVersion right)
        {
            return left == right || left > right;
        }

        public static bool operator <=((int, int, int) left, UnityVersion right)
        {
            return left == right || left < right;
        }
        #endregion
        
        private int CompareTo((int, int) other)
        {
            var result = Major.CompareTo(other.Item1);
            if (result != 0)
            {
                return result;
            }

            result = Minor.CompareTo(other.Item2);
            if (result != 0)
            {
                return result;
            }
            
            return 0;
        }

        private int CompareTo((int, int, int) other)
        {
            var result = CompareTo((other.Item1, other.Item2));
            if (result != 0)
            {
                return result;
            }
            
            result = Patch.CompareTo(other.Item3);
            if (result != 0)
            {
                return result;
            }

            return 0;
        }

        private int CompareTo(UnityVersion other)
        {
            return CompareTo((other.Major, other.Minor, other.Patch));
        }

        private bool Equals(UnityVersion other)
        {
            return (Major, Minor, Patch) == (other.Major, other.Minor, other.Patch);
        }

        public override bool Equals(object other)
        {
            return other is UnityVersion otherUnityVer && Equals(otherUnityVer);
        }

        public override int GetHashCode()
        {
            var result = Major * 31;
            result = result * 31 + Minor;
            result = result * 31 + Patch;
            result = result * 31 + Build;

            return result.GetHashCode();
        }

        public int CompareTo(object obj)
        {
            return CompareTo((UnityVersion)obj);
        }

        public sealed override string ToString()
        {
            return FullVersion;
        }

        public Tuple<int, int, int> ToTuple()
        {
            return new Tuple<int, int, int>(Major, Minor, Patch);
        }

        public int[] ToArray()
        {
            return new[] {Major, Minor, Patch};
        }
    }

    public static class UnityVersionExtensions
    {
        /// <summary>
        /// Checks if the Unity version is within the range limits specified by the "lowerLimit" and "upperLimit" attributes.
        /// </summary>
        /// <param name="ver"></param>
        /// <param name="lowerLimit">Minimal version. Included in the range.</param>
        /// <param name="upperLimit">Maximal version. Not included in the range.</param>
        /// <returns><see langword="true"/> if the Unity version is within the specified range; otherwise <see langword="false"/>.</returns>
        /// <remarks>[lowerLimit, upperLimit)</remarks>
        public static bool IsInRange(this UnityVersion ver, UnityVersion lowerLimit, UnityVersion upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, int lowerLimit, UnityVersion upperLimit)
        {
            return ver.Major >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int) lowerLimit, UnityVersion upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int, int) lowerLimit, UnityVersion upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, UnityVersion lowerLimit, int upperLimit)
        {
            return ver >= lowerLimit && ver.Major < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, UnityVersion lowerLimit, (int, int) upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, UnityVersion lowerLimit, (int, int, int) upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, int lowerLimit, int upperLimit)
        {
            return ver.Major >= lowerLimit && ver.Major < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, int lowerLimit, (int, int) upperLimit)
        {
            return ver.Major >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, int lowerLimit, (int, int, int) upperLimit)
        {
            return ver.Major >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int) lowerLimit, int upperLimit)
        {
            return ver >= lowerLimit && ver.Major < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int, int) lowerLimit, int upperLimit)
        {
            return ver >= lowerLimit && ver.Major < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int) lowerLimit, (int, int) upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int) lowerLimit, (int, int, int) upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int, int) lowerLimit, (int, int) upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }

        public static bool IsInRange(this UnityVersion ver, (int, int, int) lowerLimit, (int, int, int) upperLimit)
        {
            return ver >= lowerLimit && ver < upperLimit;
        }
    }
}
