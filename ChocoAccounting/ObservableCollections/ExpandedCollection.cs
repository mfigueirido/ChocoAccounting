/*
    This file is part of Choco Accounting.

    Choco Accounting is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Choco Accounting is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Choco Accounting. If not, see <http://www.gnu.org/licenses/>.
*/
using ChocoAccounting.DbModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ChocoAccounting.ObservableCollections
{
    /// <summary>
    /// Expands functionality of ObservableCollection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExpandedCollection<T> : ObservableCollection<T>
    {
        NotifyCollectionChangedEventArgs _forceOnCollectionChangedEventArgs =
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

        public MovementsModel Context { get; protected set; }

        public ExpandedCollection(IEnumerable<T> elements, MovementsModel context)
            : base(elements)
        {
            Context = context;
        }

        /// <summary>
        /// Needed to refresh data on DataGrid groups
        /// when changing values of calculated properties.
        /// I know it's kinda hacky.
        /// </summary>
        public void ForceOnCollectionChanged()
        {
            OnCollectionChanged(_forceOnCollectionChangedEventArgs);
        }
    }
}
