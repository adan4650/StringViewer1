using System.Collections;
using System.Collections.Generic;

namespace StringViewer1
{
    public class PagedFileCollection : IList<PageViewModel>
    {
        private readonly PageProvider _provider;
        private readonly Dictionary<long, PageViewModel> _map = new();

        public PagedFileCollection(PageProvider provider)
        {
            _provider = provider;
        }

        public PageViewModel this[int index]
        {
            get
            {
                if (!_map.TryGetValue(index, out var vm))
                {
                    vm = new PageViewModel(_provider, index);
                    _map[index] = vm;
                    vm.EnsureLoadedAsync();
                }
                return vm;
            }
            set => throw new System.NotSupportedException();
        }

        public int Count => (int)_provider.PageCount;
        public bool IsReadOnly => true;

        public IEnumerator<PageViewModel> GetEnumerator()
        {
            for (int i = 0; i < Count; i++) yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region Unsupported IList methods
        public int IndexOf(PageViewModel item) => throw new System.NotSupportedException();
        public void Insert(int index, PageViewModel item) => throw new System.NotSupportedException();
        public void RemoveAt(int index) => throw new System.NotSupportedException();
        public void Add(PageViewModel item) => throw new System.NotSupportedException();
        public void Clear() => throw new System.NotSupportedException();
        public bool Contains(PageViewModel item) => throw new System.NotSupportedException();
        public void CopyTo(PageViewModel[] array, int arrayIndex) => throw new System.NotSupportedException();
        public bool Remove(PageViewModel item) => throw new System.NotSupportedException();
        #endregion
    }
}