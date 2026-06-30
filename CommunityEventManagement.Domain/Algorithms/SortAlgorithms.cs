namespace CommunityEventManagement.Domain.Algorithms
{
   
    public static class SortAlgorithms
    {
        
        public static void InsertionSort<T, TKey>(
            IList<T> list,
            Func<T, TKey> keySelector,
            bool descending = false)
            where TKey : IComparable<TKey>
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            for (int i = 1; i < list.Count; i++)
            {
                // Pick the current element to be inserted
                T current = list[i];
                TKey currentKey = keySelector(current);
                int j = i - 1;

                // Shift elements that are greater than current to one position ahead
                while (j >= 0 && ShouldSwap(keySelector(list[j]), currentKey, descending))
                {
                    list[j + 1] = list[j];
                    j--;
                }

                // Insert current element into its correct position
                list[j + 1] = current;
            }
        }

        
        public static void InsertionSort<T>(
            IList<T> list,
            Func<T, int> keySelector,
            bool descending = false)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            for (int i = 1; i < list.Count; i++)
            {
                T current = list[i];
                int currentKey = keySelector(current);
                int j = i - 1;

                while (j >= 0)
                {
                    int leftKey = keySelector(list[j]);
                    bool shouldMove = descending ? leftKey < currentKey : leftKey > currentKey;

                    if (!shouldMove) break;

                    list[j + 1] = list[j];
                    j--;
                }

                list[j + 1] = current;
            }
        }

       
        public static void BubbleSort<T, TKey>(
            IList<T> list,
            Func<T, TKey> keySelector,
            bool descending = false)
            where TKey : IComparable<TKey>
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            int n = list.Count;
            bool swapped;

            for (int i = 0; i < n - 1; i++)
            {
                swapped = false;

                for (int j = 0; j < n - i - 1; j++)
                {
                    if (ShouldSwap(keySelector(list[j]), keySelector(list[j + 1]), descending))
                    {
                        // Swap adjacent elements
                        (list[j], list[j + 1]) = (list[j + 1], list[j]);
                        swapped = true;
                    }
                }

                // Optimisation: if no swap occurred in this pass,
                // the list is already sorted — exit early
                if (!swapped) break;
            }
        }

        
        public static List<T> GetSorted<T, TKey>(
            IEnumerable<T> source,
            Func<T, TKey> keySelector,
            bool descending = false)
            where TKey : IComparable<TKey>
        {
            var result = source.ToList();
            InsertionSort(result, keySelector, descending);
            return result;
        }

        /// <summary>
        /// Returns a NEW sorted list by integer key.
        /// Overloaded version for integer keys.
        /// </summary>
        public static List<T> GetSortedById<T>(
            IEnumerable<T> source,
            Func<T, int> idSelector,
            bool descending = false)
        {
            var result = source.ToList();
            InsertionSort(result, idSelector, descending);
            return result;
        }

        // Private helper — determines if two keys are out of order
        private static bool ShouldSwap<TKey>(TKey left, TKey right, bool descending)
            where TKey : IComparable<TKey>
        {
            int comparison = left.CompareTo(right);
            return descending ? comparison < 0 : comparison > 0;
        }
    }
}