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
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace ChocoAccounting
{
    public class GroupQuantitySumConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var movementContainer = (ReadOnlyObservableCollection<object>)values[0];
            decimal sum = 0;

            if (movementContainer[0] != null && movementContainer[0] is CollectionViewGroup)
            {
                foreach (CollectionViewGroup group in movementContainer)
                    sum += SumQuantity(group.Items);
            }
            else
            {
                sum += SumQuantity(movementContainer);
            }

            return sum.ToString("N2", TranslationSource.Instance.CurrentCulture);
        }

        private decimal SumQuantity(ReadOnlyObservableCollection<object> items)
        {
            decimal sum = 0;

            if (items.Count > 0)
            {
                foreach (object item in items)
                {
                    Movement movement = item as Movement;

                    if (movement != null)
                    {
                        SumMovementQuantity(movement, ref sum);
                    }
                    else
                    {
                        CollectionViewGroup group = item as CollectionViewGroup;

                        foreach (Movement innerMovement in group.Items)
                            SumMovementQuantity(innerMovement, ref sum);
                    }
                }
            }

            return sum;
        }

        private void SumMovementQuantity(Movement movement, ref decimal sum)
        {
            // Movement type records are fixed
            // Id 1 is expense, Id 2 is income
            if (movement.MovementTypeId == 1)
                sum -= movement.Quantity;
            else
                sum += movement.Quantity;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
