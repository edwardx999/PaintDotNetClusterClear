using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// This class was copied from the decompiled output in ILSpy.

namespace System.ServiceModel
{
    /// <summary>Provides a thread-safe collection that contains objects of a type specified by the generic parameter as elements.</summary>
    /// <typeparam name="T">The type of object contained as items in the thread-safe collection.</typeparam>
    [ComVisible(false)]
    public class SynchronizedCollection<T>
            : IList<T>,
            ICollection<T>,
            IEnumerable<T>,
            IEnumerable,
            IList,
            ICollection
    {
        private List<T> items;

        private object sync;

        /// <summary>Gets the number of elements contained in the thread-safe collection.</summary>
        /// <returns>The number of elements contained in the thread-safe, read-only collection.</returns>
        public int Count
        {
            get
            {
                lock (this.sync)
                {
                    return this.items.Count;
                }
            }
        }

        /// <summary>Gets the list of elements contained in the thread-safe collection.</summary>
        /// <returns>The <see cref="T:System.Collections.Generic.IList`1" /> of elements that are contained in the thread-safe, read-only collection.</returns>
        protected List<T> Items => this.items;

        /// <summary>Gets the object used to synchronize access to the thread-safe collection.</summary>
        /// <returns>An object used to synchronize access to the thread-safe collection.</returns>
        public object SyncRoot => this.sync;

        /// <summary>Gets an element from the thread-safe collection with a specified index.</summary>
        /// <param name="index">The zero-based index of the element to be retrieved from the collection.</param>
        /// <returns>The object in the collection that has the specified <paramref name="index" />.</returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        public T this[int index]
        {
            get
            {
                lock (this.sync)
                {
                    return this.items[index];
                }
            }
            set
            {
                lock (this.sync)
                {
                    if (index < 0 || index >= this.items.Count)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    SetItem(index, value);
                }
            }
        }

        /// <summary>Gets a value that indicates whether the collection is read only.</summary>
        /// <returns>
        ///     <see langword="false" />
        ///   </returns>
        bool ICollection<T>.IsReadOnly => false;

        /// <summary>Gets a value that indicates whether the collection is thread safe.</summary>
        /// <returns>
        ///     <see langword="true" />
        ///   </returns>
        bool ICollection.IsSynchronized => true;

        /// <summary>Gets the object used to synchronize access to the thread-safe collection.</summary>
        /// <returns>An object used to synchronize access to the thread-safe collection.</returns>
        object ICollection.SyncRoot => this.sync;

        /// <summary>Gets or sets the item at a specified zero-based index.</summary>
        /// <param name="index">The zero-based index of the element to be retrieved from the collection.</param>
        /// <returns>The item with the specified <paramref name="index" />.</returns>
        /// <exception cref="T:System.ArgumentException">The value set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        object IList.this[int index]
        {
            get
            {
                return this[index];
            }
            set
            {
                VerifyValueType(value);
                this[index] = (T)value;
            }
        }

        /// <summary>Gets a value that indicates whether the collection is read only.</summary>
        /// <returns>
        ///     <see langword="false" />
        ///   </returns>
        bool IList.IsReadOnly => false;

        /// <summary>Gets a value that indicates whether the collection is fixed in size.</summary>
        /// <returns>
        ///     <see langword="false" />
        ///   </returns>
        bool IList.IsFixedSize => false;

        /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Generic.SynchronizedCollection`1" /> class. </summary>
        public SynchronizedCollection()
        {
            this.items = new List<T>();
            this.sync = new object();
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Generic.SynchronizedCollection`1" /> class with the object used to synchronize access to the thread-safe collection.</summary>
        /// <param name="syncRoot">The object used to synchronize access the thread-safe collection.</param>
        /// <exception cref="T:System.ArgumentNullException">
        ///         <paramref name="syncRoot" /> is <see langword="null" />.</exception>
        public SynchronizedCollection(object syncRoot)
        {
            if (syncRoot == null)
            {
                throw new ArgumentNullException("syncRoot");
            }
            this.items = new List<T>();
            this.sync = syncRoot;
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Generic.SynchronizedCollection`1" /> class from a specified enumerable list of elements and with the object used to synchronize access to the thread-safe collection.</summary>
        /// <param name="syncRoot">The object used to synchronize access to the thread-safe collection.</param>
        /// <param name="list">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> collection of elements used to initialize the thread-safe collection.</param>
        /// <exception cref="T:System.ArgumentNullException">
        ///         <paramref name="syncRoot" /> or <paramref name="list" /> is <see langword="null" />. </exception>
        public SynchronizedCollection(object syncRoot, IEnumerable<T> list)
        {
            if (syncRoot == null)
            {
                throw new ArgumentNullException("syncRoot");
            }
            if (list == null)
            {
                throw new ArgumentNullException("list");
            }
            this.items = new List<T>(list);
            this.sync = syncRoot;
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Generic.SynchronizedCollection`1" /> class from a specified array of elements and with the object used to synchronize access to the thread-safe collection.</summary>
        /// <param name="syncRoot">The object used to synchronize access the thread-safe collection.</param>
        /// <param name="list">The <see cref="T:System.Array" /> of type <paramref name="T" /> elements used to initialize the thread-safe collection.</param>
        /// <exception cref="T:System.ArgumentNullException">
        ///         <paramref name="syncRoot" /> or <paramref name="list" /> is <see langword="null" />. </exception>
        public SynchronizedCollection(object syncRoot, params T[] list)
        {
            if (syncRoot == null)
            {
                throw new ArgumentNullException("syncRoot");
            }
            if (list == null)
            {
                throw new ArgumentNullException("list");
            }
            this.items = new List<T>(list.Length);
            for (int i = 0; i < list.Length; i++)
            {
                this.items.Add(list[i]);
            }
            this.sync = syncRoot;
        }

        /// <summary>Adds an item to the thread-safe, read-only collection.</summary>
        /// <param name="item">The element to be added to the collection.</param>
        /// <exception cref="T:System.ArgumentException">The value set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        public void Add(T item)
        {
            lock (this.sync)
            {
                int count = this.items.Count;
                InsertItem(count, item);
            }
        }

        /// <summary>Removes all items from the collection.</summary>
        public void Clear()
        {
            lock (this.sync)
            {
                ClearItems();
            }
        }

        /// <summary>Copies the elements of the collection to a specified array, starting at a particular index.</summary>
        /// <param name="array">The destination <see cref="T:System.Array" /> for the elements of type <paramref name="T " />copied from the collection.</param>
        /// <param name="index">The zero-based index in the array at which copying begins.</param>
        public void CopyTo(T[] array, int index)
        {
            lock (this.sync)
            {
                this.items.CopyTo(array, index);
            }
        }

        /// <summary>Determines whether the collection contains an element with a specific value.</summary>
        /// <param name="item">The object to locate in the collection.</param>
        /// <returns>
        ///     <see langword="true" /> if the element value is found in the collection; otherwise<see langword=" false" />.</returns>
        /// <exception cref="T:System.ArgumentException">The value set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        public bool Contains(T item)
        {
            lock (this.sync)
            {
                return this.items.Contains(item);
            }
        }

        /// <summary>Returns an enumerator that iterates through the synchronized collection.</summary>
        /// <returns>An <see cref="T:System.Collections.Generic.IEnumerator`1" /> for objects of the type stored in the collection.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            lock (this.sync)
            {
                return this.items.GetEnumerator();
            }
        }

        /// <summary>Returns the index of the first occurrence of a value in the collection.</summary>
        /// <param name="item">Removes all items from the collection.</param>
        /// <returns>The zero-based index of the first occurrence of the value in the collection.</returns>
        /// <exception cref="T:System.ArgumentException">The value set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        public int IndexOf(T item)
        {
            lock (this.sync)
            {
                return InternalIndexOf(item);
            }
        }

        /// <summary>Inserts an item into the collection at a specified index.</summary>
        /// <param name="index">The zero-based index of the element to be retrieved from the collection.</param>
        /// <param name="item">The object to be inserted into the collection as an element.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        /// <exception cref="T:System.ArgumentException">The value set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        public void Insert(int index, T item)
        {
            lock (this.sync)
            {
                if (index < 0 || index > this.items.Count)
                {
                    throw new ArgumentOutOfRangeException("index");
                }
                InsertItem(index, item);
            }
        }

        private int InternalIndexOf(T item)
        {
            int count = this.items.Count;
            for (int i = 0; i < count; i++)
            {
                if (object.Equals(this.items[i], item))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Removes the first occurrence of a specified item from the collection.</summary>
        /// <param name="item">The object to remove from the collection.</param>
        /// <returns>
        ///     <see langword="true" /> if item was successfully removed from the collection; otherwise, <see langword="false" />.</returns>
        public bool Remove(T item)
        {
            lock (this.sync)
            {
                int num = InternalIndexOf(item);
                if (num < 0)
                {
                    return false;
                }
                RemoveItem(num);
                return true;
            }
        }

        /// <summary>Removes an item at a specified index from the collection.</summary>
        /// <param name="index">The zero-based index of the element to be retrieved from the collection.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        public void RemoveAt(int index)
        {
            lock (this.sync)
            {
                if (index < 0 || index >= this.items.Count)
                {
                    throw new ArgumentOutOfRangeException("index");
                }
                RemoveItem(index);
            }
        }

        /// <summary>Removes all items from the collection.</summary>
        protected virtual void ClearItems()
        {
            this.items.Clear();
        }

        /// <summary>Inserts an item into the collection at a specified index.</summary>
        /// <param name="index">The zero-based index of the collection where the object is to be inserted.</param>
        /// <param name="item">The object to be inserted into the collection.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        /// <exception cref="T:System.ArgumentException">The value set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        protected virtual void InsertItem(int index, T item)
        {
            this.items.Insert(index, item);
        }

        /// <summary>Removes an item at a specified <paramref name="index" /> from the collection.</summary>
        /// <param name="index">The zero-based index of the element to be retrieved from the collection.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        protected virtual void RemoveItem(int index)
        {
            this.items.RemoveAt(index);
        }

        /// <summary>Replaces the item at a specified index with another item.</summary>
        /// <param name="index">The zero-based index of the object to be replaced.</param>
        /// <param name="item">The object to replace </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        protected virtual void SetItem(int index, T item)
        {
            this.items[index] = item;
        }

        /// <summary>Returns an enumerator that iterates through the synchronized collection.</summary>
        /// <returns>An <see cref="T:System.Collections.Generic.IEnumerator`1" /> for objects of the type stored in the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.items).GetEnumerator();
        }

        /// <summary>Copies the elements of the collection to a specified array, starting at a particular index.</summary>
        /// <param name="array">The destination <see cref="T:System.Array" /> for the elements of type <paramref name="T" /> copied from the collection.</param>
        /// <param name="index">The zero-based index in the array at which copying begins.</param>
        void ICollection.CopyTo(Array array, int index)
        {
            lock (this.sync)
            {
                ((ICollection)this.items).CopyTo(array, index);
            }
        }

        /// <summary>Adds an element to the collection.</summary>
        /// <param name="value">The object to add to the collection.</param>
        /// <returns>The position into which the new element was inserted.</returns>
        int IList.Add(object value)
        {
            VerifyValueType(value);
            lock (this.sync)
            {
                Add((T)value);
                return this.Count - 1;
            }
        }

        /// <summary>Determines whether the collection contains an element with a specific value.</summary>
        /// <param name="value">The object to locate in the collection.</param>
        /// <returns>
        ///     <see langword="true" /> if the element <paramref name="value" /> is found in the collection; otherwise<see langword=" false" />.</returns>
        /// <exception cref="T:System.ArgumentException">
        ///         <paramref name="value" /> is not an object of the type contained in the collection.</exception>
        bool IList.Contains(object value)
        {
            VerifyValueType(value);
            return Contains((T)value);
        }

        /// <summary>Determines the zero-based index of an element in the collection.</summary>
        /// <param name="value">The element in the collection whose index is being determined.</param>
        /// <returns>The index of the <paramref name="value" /> if found in the collection; otherwise, -1.</returns>
        int IList.IndexOf(object value)
        {
            VerifyValueType(value);
            return IndexOf((T)value);
        }

        /// <summary>Inserts an object into the collection at a specified index.</summary>
        /// <param name="index">The zero-based index at which <paramref name="value" /> is to be inserted.</param>
        /// <param name="value">The object to insert into the collection.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="index" /> specified is less than zero or greater than the number of items in the collection.</exception>
        /// <exception cref="T:System.ArgumentException">The <paramref name="value" /> set is <see langword="null" /> or is not of the correct generic type <paramref name="T" /> for the collection.</exception>
        void IList.Insert(int index, object value)
        {
            VerifyValueType(value);
            Insert(index, (T)value);
        }

        /// <summary>Removes the first occurrence of a specified object as an element from the collection.</summary>
        /// <param name="value">The object to be removed from the collection.</param>
        void IList.Remove(object value)
        {
            VerifyValueType(value);
            Remove((T)value);
        }

        private static void VerifyValueType(object value)
        {
            if (value == null)
            {
                if (typeof(T).IsValueType)
                {
                    throw new ArgumentException();
                }
            }
            else if (!(value is T))
            {
                throw new ArgumentException();
            }
        }
    }

}