using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;

namespace Couchbase.Utils
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Provides random number generation for array randomization
        /// </summary>
        internal static Random Random = new Random();

        public static void Shuffle(this Array array)
        {
            var length = array.Length;
            while (length > 1)
            {
                length--;
                var index = Random.Next(length + 1);
                var item = array.GetValue(index);
                array.SetValue(array.GetValue(length), index);
                array.SetValue(item, length);
            }
        }

        public static List<T> Shuffle<T>(this List<T> list)
        {
            var length = list.Count;
            while (length > 1)
            {
                length--;
                var index = Random.Next(length + 1);
                var item = list[index];
                list[index] = list[length];
                list[length] = item;
            }
            return list;
        }

        public static T GetRandom<T>(this IEnumerable<T> list)
        {
            var item = default(T);

            var enumerable = list as IList<T> ?? list.ToList();
            if (enumerable.Any())
            {
                var index = Random.Next(enumerable.Count);
                item = enumerable[index];
            }

            return item;
        }

        public static T GetRandom<T>(this IEnumerable<T> enumerable, Func<T, bool> whereClause)
        {
            var item = default(T);

            var list = enumerable as IList<T> ?? enumerable.Where(whereClause).ToList();
            if (list.Any())
            {
                var index = Random.Next(list.Count);
                item = list[index];
            }

            return item;
        }

        public static bool AreEqual<T>(this List<T> array, List<T> other)
        {
            if (array == null && other == null) return true;
            if (array == null) return false;
            if (other == null) return false;
           
            return array.Count == other.Count && array.SequenceEqual(other);
        }

        public static bool AreEqual<T>(this Array array, Array other)
        {
            return (other != null &&
                CompareItems<T>(array, other));
        }

        static bool CompareItems<T>(this Array array, Array other)
        {
            return array.Rank == other.Rank &&
                   Enumerable.Range(0, array.Rank).
                   All(dim => array.GetLength(dim) == other.GetLength(dim)) &&
                   array.Cast<T>().SequenceEqual(other.Cast<T>());
        }

        public static bool AreEqual(this short[][] array, short[][] other)
        {
            if (array == null && other == null)
            {
                return true;
            }
            if (array != null && other == null)
            {
                return false;
            }

            if (array?.Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < array.Length; i++)
            {
                for (var j = 0; j < array[j].Length; j++)
                {
                    if (array[i][j] != other[i][j])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static int GetCombinedHashcode(this Array array)
        {
            unchecked
            {
                var hash = 0;
                var count = 0;
                foreach (var item in array)
                {
                    if (item.GetType().GetTypeInfo().BaseType == typeof (Array))
                    {
                        var jagged = (Array)item;
                        foreach (var inner in jagged)
                        {
                            hash += inner.GetHashCode();
                            count++;
                        }
                    }
                    else
                    {
                        hash += item.GetHashCode();
                        count++;
                    }
                }
                return 31*hash + count.GetHashCode();
            }
        }

        /// <summary>
        /// Converts an array to a JSON string.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <returns></returns>
        public static string ToJson(this IEnumerable array)
        {
            return JsonConvert.SerializeObject(array);
        }

        /// <summary>
        /// Converts an array to a JSON string and optionally strips the begining and ending brackets.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="stripBrackets">if set to <c>true</c> the brackets '[' and ']' will be removed.</param>
        /// <returns>A JSON string array.</returns>
        public static string ToJson(this IEnumerable array, bool stripBrackets)
        {
            if (stripBrackets)
            {
                return ToJson(array).TrimStart('[').TrimEnd(']');
            }
            return ToJson(array);
        }

        public static ReadOnlyMemory<byte> StripBrackets(this ReadOnlyMemory<byte> theArray)
        {
            if (theArray.Length > 1 && theArray.Span[0] == 0x5b && theArray.Span[theArray.Length-1] == 0x5d)
            {
                return theArray.Slice(1, theArray.Length - 2);
            }
            return theArray;
        }

        public static bool IsJson(this Span<byte> buffer)
        {
            return ((ReadOnlySpan<byte>) buffer).IsJson();
        }

        public static bool IsJson(this ReadOnlySpan<byte> buffer)
        {
            return (buffer.Length > 1 && buffer[0] == 0x5b && buffer[buffer.Length-1] == 0x5d) ||
                   (buffer.Length > 1 && buffer[0] == 0x7b && buffer[buffer.Length-1] == 0x7d);
        }

        /// <summary>Creates a string from a list with each value delimited by the value of <see cref="delimiter"/> and
        /// each value "N1QL escaped" by backticks "`".
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="theArray">The array to construct the string from.</param>
        /// <param name="delimiter">The value to delimit each value by.</param>
        /// <returns>A string of the values of the array delimited by the <see cref="delimiter"/> and enclosed with backticks.</returns>
        // ReSharper disable once InconsistentNaming
        public static string ToDelimitedN1QLString<T>(this T[] theArray, char delimiter)
        {
            var theString = string.Empty;
            for (var i = 0; i < theArray.Length; i++)
            {
                theString += theArray[i].ToString().N1QlEscape();
                if (i != theArray.Length - 1)
                {
                    theString += string.Concat(delimiter, " ");
                }
            }
            return theString;
        }
    }
}

