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
using System.Data.Entity.Infrastructure;

namespace ChocoAccounting.Migrations
{
    public class MigrationsContextFactory : IDbContextFactory<MovementsModel>
    {
        public MovementsModel Create()
        {
            return new MovementsModel(@"data source=(LocalDB)\MSSQLLocalDB;initial catalog=Default;AttachDbFilename=|DataDirectory|\Default.mdf;integrated security=True;MultipleActiveResultSets=True;App=EntityFramework");
        }
    }
}
