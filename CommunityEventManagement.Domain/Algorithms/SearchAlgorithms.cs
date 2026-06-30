namespace CommunityEventManagement.Domain.Algorithms
{

    public static class SearchAlgorithms
    {
       
        public static int BinarySearch<T>(
            IList<T> sortedList,
            int target,
            Func<T, int> keySelector)
        {
            if (sortedList == null)
                throw new ArgumentNullException(nameof(sortedList));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            int left = 0;
            int right = sortedList.Count - 1;

            while (left <= right)
            {
                // Calculate middle index — avoids integer overflow
                int middle = left + (right - left) / 2;
                int middleKey = keySelector(sortedList[middle]);

                if (middleKey == target)
                {
                    // Found — return the index
                    return middle;
                }
                else if (middleKey < target)
                {
                    // Target is in the RIGHT half — discard left half
                    left = middle + 1;
                }
                else
                {
                    // Target is in the LEFT half — discard right half
                    right = middle - 1;
                }
            }

            // Not found
            return -1;
        }

       
        public static int BinarySearch<T>(
            IList<T> sortedList,
            string target,
            Func<T, string> keySelector,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (sortedList == null)
                throw new ArgumentNullException(nameof(sortedList));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            int left = 0;
            int right = sortedList.Count - 1;

            while (left <= right)
            {
                int middle = left + (right - left) / 2;
                string middleKey = keySelector(sortedList[middle]);

                int compareResult = string.Compare(middleKey, target, comparison);

                if (compareResult == 0)
                {
                    return middle;
                }
                else if (compareResult < 0)
                {
                    left = middle + 1;
                }
                else
                {
                    right = middle - 1;
                }
            }

            return -1;
        }

        
        public static int LinearSearch<T>(
            IList<T> list,
            int target,
            Func<T, int> keySelector)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            for (int i = 0; i < list.Count; i++)
            {
                if (keySelector(list[i]) == target)
                    return i;
            }

            return -1;
        }

       
        public static T? FindById<T>(
            IList<T> sortedList,
            int id,
            Func<T, int> idSelector)
        {
            int index = BinarySearch(sortedList, id, idSelector);
            return index >= 0 ? sortedList[index] : default;
        }
    }
}