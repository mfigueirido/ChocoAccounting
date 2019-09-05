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
using ChocoAccounting.ObservableCollections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChocoAccounting
{
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string _databasePath;
        bool _dataRefreshEnabled = true;
        bool _validateOnValueChanged = true;
        bool _loadingComboBoxItems = false;

        TimeSpan _month = new TimeSpan(30, 0, 0, 0);
        MonthNameConverter _monthNameConverter = new MonthNameConverter();
        DateCultureConverter _dateCultureConverter = new DateCultureConverter();
        SubcategoryNameConverter _subcategoryNameConverter = new SubcategoryNameConverter();
        EntityNameConverter _entityNameConverter = new EntityNameConverter();
        PersonNameConverter _personNameConverter = new PersonNameConverter();

        MovementsModel _ctx;
        MovementCollection _movements;
        AccountCollection _accounts;
        MovementCategoryCollection _categories;
        EntityCollection _entities;
        PersonCollection _persons;
        MovementSubcategoryCollection _editableSubcategories;

        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        const string DecimalRegex = "[^0-9.,]+";

        public MainWindow()
        {
            InitializeComponent();

            ApplySettings();

            InitializeDatabase();

            SetAppTitle();

            LoadStaticControls();

            LoadFilteringControlsData();
            LoadSortingAndGroupingControlsData();
            LoadFieldsData();
            LoadMovementSetData();

            InitializeMovementsDataRefreshTimer();
            AssignComboComplementDelegates();
            DisableMouseWheelInput();
        }

        #region General

        private void ApplySettings()
        {
            // Upgrade settings file in case the application version changed
            Properties.Settings.Default.Upgrade();
            Properties.Settings.Default.Reload();

            // If the connection string is not configured get the app.config
            // default connection string
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.ConnectionString))
                Tools.ChangeConnectionStringConfig(Tools.GetConnectionStringAppConfig());

            CultureInfo culture;

            // If culture is not configured get the default system culture
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.Culture))
            {
                if (Thread.CurrentThread.CurrentCulture.Name.StartsWith("gl"))
                    culture = new CultureInfo("gl");
                else if (Thread.CurrentThread.CurrentCulture.Name.StartsWith("es"))
                    culture = new CultureInfo("es");
                else // Fallback language is English
                    culture = new CultureInfo("en");
            }
            else
            {
                culture = new CultureInfo(Properties.Settings.Default.Culture);
            }

            // Update culture setting
            Tools.ChangeCultureConfig(culture);
        }

        private void InitializeDatabase()
        {
            // We might need this when we start using EF migrations
            //Database.SetInitializer(new MigrateDatabaseToLatestVersion<MovementsModel, Configuration>());

            // Save databases in application data folder
            _databasePath = System.IO.Path.Combine(Environment.GetFolderPath(
                                             Environment.SpecialFolder.Applic‌​ationData),
                                             "ChocoAccounting");

            // If the folder does not exist, create it
            if (!Directory.Exists(_databasePath))
                Directory.CreateDirectory(_databasePath);

            // Set the folder as the current data directory so LocalDB uses it to store databases
            AppDomain.CurrentDomain.SetData("DataDirectory", _databasePath);

            // Create context object from EF using the current connection string
            _ctx = new MovementsModel(Properties.Settings.Default.ConnectionString);

            // Create or update non-editable records
            ManageMasterData();

            // Create default categories (if the user wants)
            CheckAndCreateDefaultCategories();

            // Load other start-up data
            LoadSharedObservableCollections();
        }

        private void ManageMasterData()
        {
            // Create or update expense type with current localization
            IQueryable<MovementType> query = _ctx.MovementTypeSet
                                        .Where(s => s.Id == 1);

            List<MovementType> list = query.ToList();

            MovementType movementType1;

            if (list.Count == 0)
            {
                movementType1 = new MovementType();
                _ctx.MovementTypeSet.Add(movementType1);
            }
            else
            {
                movementType1 = list[0];
            }

            movementType1.Name = TranslationSource.Instance["Expense"];

            // Create or update income type with current localization
            query = _ctx.MovementTypeSet
                .Where(s => s.Id == 2);

            list = query.ToList();

            MovementType movementType2;

            if (list.Count == 0)
            {
                movementType2 = new MovementType();
                _ctx.MovementTypeSet.Add(movementType2);
            }
            else
            {
                movementType2 = list[0];
            }

            movementType2.Name = TranslationSource.Instance["Income"];

            SaveDbChanges();
        }

        private void CheckAndCreateDefaultCategories()
        {
            // If there aren't any categories created
            if (_ctx.MovementCategorySet.AsQueryable().ToList().Count == 0)
            {
                if (MessageBox.Show(TranslationSource.Instance["CreateDefaultCategoriesQuestion"],
                        TranslationSource.Instance["MessageBoxAttentionText"], MessageBoxButton.YesNo)
                        == MessageBoxResult.Yes)
                {
                    MovementCategory category;
                    MovementSubcategory subcategory;

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Salaries"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["OtherIncomes"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Home"];
                    _ctx.MovementCategorySet.Add(category);
                    SaveDbChanges();

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Rentals"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["RegularShopping"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["OtherShopping"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["ManteinanceAndRepairs"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Taxes"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Insurances"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Mortgage"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);
                    SaveDbChanges();

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Loans"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Energy"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Water"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Communications"];
                    _ctx.MovementCategorySet.Add(category);
                    SaveDbChanges();

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Phone"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Mobile"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Internet"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);
                    SaveDbChanges();

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Transportation"];
                    _ctx.MovementCategorySet.Add(category);
                    SaveDbChanges();

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Fuel"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Tolls"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["ManteinanceAndRepairs"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Taxes"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Insurances"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);

                    subcategory = new MovementSubcategory();
                    subcategory.Name = TranslationSource.Instance["Fines"];
                    subcategory.MovementCategoryId = category.Id;
                    _ctx.MovementSubcategorySet.Add(subcategory);
                    SaveDbChanges();

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Clothing"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Doctor"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["ChemistsHerbalists"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Vet"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Leisure"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Donations"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Gifts"];
                    _ctx.MovementCategorySet.Add(category);

                    category = new MovementCategory();
                    category.Name = TranslationSource.Instance["Other"];
                    _ctx.MovementCategorySet.Add(category);
                    SaveDbChanges();
                }
            }
        }

        private void LoadSharedObservableCollections()
        {
            _accounts = new AccountCollection(_ctx.AccountSet.OrderBy(o => o.Name), _ctx);
            _categories = new MovementCategoryCollection(_ctx.MovementCategorySet.OrderBy(o => o.Name), _ctx);
            _entities = new EntityCollection(_ctx.EntitySet.OrderBy(o => o.Name), _ctx);
            _persons = new PersonCollection(_ctx.PersonSet.OrderBy(o => o.Name), _ctx);
        }

        private void SetAppTitle()
        {
            Title = "Choco Accounting - " + TranslationSource.Instance["Database"] + ": " + _ctx.Database.Connection.Database;
        }

        private void LoadStaticControls()
        {
            _loadingComboBoxItems = true;

            // Set category and subcategory combo language. Needed for proper 
            // decimal value input on tax percentage field.
            categoryComboBox.Language = XmlLanguage.GetLanguage(
                TranslationSource.Instance.CurrentCulture.IetfLanguageTag);

            subcategoryComboBox.Language = XmlLanguage.GetLanguage(
                TranslationSource.Instance.CurrentCulture.IetfLanguageTag);

            // Language combo in app config
            bool languageComboBoxHadItems = languageComboBox.HasItems;

            List<CultureSelectionData> cultures = new List<CultureSelectionData>
            {
                new CultureSelectionData("en", TranslationSource.Instance["English"]),
                new CultureSelectionData("gl", TranslationSource.Instance["Galician"]),
                new CultureSelectionData("es", TranslationSource.Instance["Spanish"])
            };

            int selectedIndex = languageComboBox.SelectedIndex;
            languageComboBox.DisplayMemberPath = "Name";
            languageComboBox.ItemsSource = cultures;

            // If the language combo did not have any items we assume we are at 
            // application start, so we select the value stored in config.
            if (languageComboBoxHadItems)
            {
                languageComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                CultureInfo c = TranslationSource.Instance.CurrentCulture;
                IQueryable<CultureSelectionData> query = cultures.AsQueryable()
                    .Where(a => a.Culture.Name == c.Name);
                List<CultureSelectionData> list = query.ToList();
                languageComboBox.SelectedItem = list[0];
            }

            // Pending combos (filter and edit sections)
            List<string> yesNoItems = new List<string>
            {
                TranslationSource.Instance["No"],
                TranslationSource.Instance["Yes"]
            };

            selectedIndex = pendingComboBoxFilter.SelectedIndex;
            pendingComboBoxFilter.ItemsSource = yesNoItems;
            pendingComboBoxFilter.SelectedIndex = selectedIndex;

            bool pendingComboBoxHadItems = pendingComboBox.HasItems;
            selectedIndex = pendingComboBox.SelectedIndex;
            pendingComboBox.ItemsSource = yesNoItems;
            pendingComboBox.SelectedIndex = selectedIndex;

            // If the pending combo did not have any items we assume we are at 
            // application start, so we select the first element 'No' as default
            if (pendingComboBoxHadItems)
                pendingComboBox.SelectedIndex = selectedIndex;
            else
                pendingComboBox.SelectedIndex = 0;

            _loadingComboBoxItems = false;

            ManageMasterData();

            // Type controls (filter and edit sections)
            selectedIndex = typeListBoxFilter.SelectedIndex;

            Tools.SetSelectorData(typeListBoxFilter, _ctx.MovementTypeSet, "Name", "Id");

            if (typeListBoxFilter.HasItems)
                typeListBoxFilter.SelectedIndex = selectedIndex;

            selectedIndex = typeComboBox.SelectedIndex;

            Tools.SetSelectorData(typeComboBox, _ctx.MovementTypeSet, "Name", "Id");

            if (typeComboBox.HasItems)
                typeComboBox.SelectedIndex = selectedIndex;

            // Date picker watermarks
            Tools.ReloadDatePickerWatermark(fromDatePickerFilter);
            Tools.ReloadDatePickerWatermark(toDatePickerFilter);
            Tools.ReloadDatePickerWatermark(datePicker);
        }

        private void LoadMovementSetData()
        {
            if (_dataRefreshEnabled)
            {
                var query = _ctx.MovementSet.AsQueryable();

                query = AddFilteringToQuery(query);
                query = AddSortingToQuery(query);

                // Consolidate entities in memory before grouping
                // so we can group by calculated fields
                query = query.ToList().AsQueryable();

                List<GroupingInfo> groups = new List<GroupingInfo>(3);

                groups.Add(new GroupingInfo());
                groups.Add(new GroupingInfo());
                groups.Add(new GroupingInfo());

                groups[0].Control = group1ComboBox;
                groups[1].Control = group2ComboBox;
                groups[2].Control = group3ComboBox;

                query = AddGroupingToQuery(query, ref groups);

                // Encapsulate the result in an ObservableCollection to 
                // enable context tracking
                _movements = new MovementCollection(query, _ctx);

                // Assign an ObservableCollection or a CollectionView
                // to the DataGrid depending on if there are grouping operations
                if (string.IsNullOrEmpty(groups[0].Member)
                    && string.IsNullOrEmpty(groups[1].Member)
                    && string.IsNullOrEmpty(groups[2].Member))
                {
                    dataGrid.DataContext = _movements;
                    dataGrid.ItemsSource = _movements;
                }
                else
                {
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(_movements);

                    if (!string.IsNullOrEmpty(groups[0].Member))
                        view.GroupDescriptions.Add(new PropertyGroupDescription(groups[0].Member,
                                                    groups[0].Converter));

                    if (!string.IsNullOrEmpty(groups[1].Member))
                        view.GroupDescriptions.Add(new PropertyGroupDescription(groups[1].Member,
                                                    groups[1].Converter));

                    if (!string.IsNullOrEmpty(groups[2].Member))
                        view.GroupDescriptions.Add(new PropertyGroupDescription(groups[2].Member,
                                                    groups[2].Converter));

                    dataGrid.DataContext = view;
                    dataGrid.ItemsSource = view;
                }
            }
        }

        private IQueryable<Movement> AddFilteringToQuery(IQueryable<Movement> query)
        {
            query = query.Where(m => m.Date >= fromDatePickerFilter.SelectedDate
                                && m.Date <= toDatePickerFilter.SelectedDate);

            if (accountListBoxFilter.SelectedItems.Count > 0)
            {
                List<int> accountIds = new List<int>();

                foreach (Account account in accountListBoxFilter.SelectedItems)
                    accountIds.Add(account.Id);

                query = query.Where(m => accountIds.Contains(m.AccountId));
            }

            if (typeListBoxFilter.SelectedItems.Count > 0)
            {
                List<int> typeIds = new List<int>();

                foreach (MovementType type in typeListBoxFilter.SelectedItems)
                    typeIds.Add(type.Id);

                query = query.Where(m => typeIds.Contains(m.MovementTypeId));
            }

            if (categoryListBoxFilter.SelectedItems.Count > 0)
            {
                List<int> categoryIds = new List<int>();

                foreach (MovementCategory category in categoryListBoxFilter.SelectedItems)
                    categoryIds.Add(category.Id);

                query = query.Where(m => categoryIds.Contains(m.MovementCategoryId));
            }

            if (subcategoryListBoxFilter.SelectedItems.Count > 0)
            {
                List<int?> subcategoryIds = new List<int?>();

                foreach (MovementSubcategory subcategory in subcategoryListBoxFilter.SelectedItems)
                    subcategoryIds.Add(subcategory.Id);

                query = query.Where(m => subcategoryIds.Contains(m.MovementSubcategoryId));
            }

            if (entityListBoxFilter.SelectedItems.Count > 0)
            {
                List<int?> entityIds = new List<int?>();

                foreach (Entity entity in entityListBoxFilter.SelectedItems)
                    entityIds.Add(entity.Id);

                query = query.Where(m => entityIds.Contains(m.EntityId));
            }

            if (personListBoxFilter.SelectedItems.Count > 0)
            {
                List<int?> personIds = new List<int?>();

                foreach (Person person in personListBoxFilter.SelectedItems)
                    personIds.Add(person.Id);

                query = query.Where(m => personIds.Contains(m.PersonId));
            }

            if (!string.IsNullOrEmpty(remarksTextBoxFilter.Text))
                query = query.Where(m => m.Remarks.Contains(remarksTextBoxFilter.Text));

            if (pendingComboBoxFilter.SelectedValue != null)
            {
                bool pending = pendingComboBoxFilter.SelectedIndex == 0 ? false : true;

                if (pendingComboBoxFilter.SelectedValue != null)
                    query = query.Where(m => m.Pending == pending);
            }

            return query;
        }

        private IQueryable<Movement> AddSortingToQuery(IQueryable<Movement> query)
        {
            IOrderedQueryable<Movement> orderedQuery = (IOrderedQueryable<Movement>)query;

            bool firstSort = true;

            if (sort1ComboBox.SelectedIndex != -1)
            {
                orderedQuery = AddSortingOperationToQuery(orderedQuery, sort1ComboBox.SelectedIndex,
                    sort1KindComboBox.SelectedIndex == 0 ? false : true, firstSort);
                firstSort = false;
            }

            if (sort2ComboBox.SelectedIndex != -1)
            {
                orderedQuery = AddSortingOperationToQuery(orderedQuery, sort2ComboBox.SelectedIndex,
                    sort2KindComboBox.SelectedIndex == 0 ? false : true, firstSort);
                firstSort = false;
            }

            if (sort3ComboBox.SelectedIndex != -1)
            {
                orderedQuery = AddSortingOperationToQuery(orderedQuery, sort3ComboBox.SelectedIndex,
                    sort3KindComboBox.SelectedIndex == 0 ? false : true, firstSort);
                firstSort = false;
            }

            // Internal sorting, always sort by id
            if (firstSort)
                orderedQuery = orderedQuery.OrderByDescending(m => m.Id);
            else
                orderedQuery = orderedQuery.ThenByDescending(m => m.Id);

            return orderedQuery;
        }

        private IOrderedQueryable<Movement> AddSortingOperationToQuery(IOrderedQueryable<Movement> query,
            int fieldIndex, bool descending, bool first)
        {
            switch (fieldIndex)
            {
                case 0:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Date.Year);
                        else
                            query = query.OrderBy(m => m.Date.Year);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Date.Year);
                        else
                            query = query.ThenBy(m => m.Date.Year);
                    }
                    break;

                case 1:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Date.Month);
                        else
                            query = query.OrderBy(m => m.Date.Month);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Date.Month);
                        else
                            query = query.ThenBy(m => m.Date.Month);
                    }
                    break;

                case 2:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Date);
                        else
                            query = query.OrderBy(m => m.Date);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Date);
                        else
                            query = query.ThenBy(m => m.Date);
                    }
                    break;

                case 3:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Account.Name);
                        else
                            query = query.OrderBy(m => m.Account.Name);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Account.Name);
                        else
                            query = query.ThenBy(m => m.Account.Name);
                    }
                    break;

                case 4:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.MovementType.Name);
                        else
                            query = query.OrderBy(m => m.MovementType.Name);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.MovementType.Name);
                        else
                            query = query.ThenBy(m => m.MovementType.Name);
                    }
                    break;

                case 5:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.MovementCategory.Name);
                        else
                            query = query.OrderBy(m => m.MovementCategory.Name);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.MovementCategory.Name);
                        else
                            query = query.ThenBy(m => m.MovementCategory.Name);
                    }
                    break;

                case 6:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.MovementSubcategory.Name);
                        else
                            query = query.OrderBy(m => m.MovementSubcategory.Name);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.MovementSubcategory.Name);
                        else
                            query = query.ThenBy(m => m.MovementSubcategory.Name);
                    }
                    break;

                case 7:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Entity.Name);
                        else
                            query = query.OrderBy(m => m.Entity.Name);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Entity.Name);
                        else
                            query = query.ThenBy(m => m.Entity.Name);
                    }
                    break;

                case 8:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Person.Name);
                        else
                            query = query.OrderBy(m => m.Person.Name);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Person.Name);
                        else
                            query = query.ThenBy(m => m.Person.Name);
                    }
                    break;

                case 9:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Quantity);
                        else
                            query = query.OrderBy(m => m.Quantity);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Quantity);
                        else
                            query = query.ThenBy(m => m.Quantity);
                    }
                    break;

                case 10:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Remarks);
                        else
                            query = query.OrderBy(m => m.Remarks);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Remarks);
                        else
                            query = query.ThenBy(m => m.Remarks);
                    }
                    break;

                case 11:
                    if (first)
                    {
                        if (descending)
                            query = query.OrderByDescending(m => m.Pending);
                        else
                            query = query.OrderBy(m => m.Pending);
                    }
                    else
                    {
                        if (descending)
                            query = query.ThenByDescending(m => m.Pending);
                        else
                            query = query.ThenBy(m => m.Pending);
                    }
                    break;
            }

            return query;
        }

        private IQueryable<Movement> AddGroupingToQuery(IQueryable<Movement> query, ref List<GroupingInfo> groups)
        {
            foreach (GroupingInfo group in groups)
            {
                switch (group.Control.SelectedIndex)
                {
                    case 0:
                        group.Member = "Year";
                        query = query.GroupBy(m => m.Year).SelectMany(e => e);

                        break;

                    case 1:
                        group.Member = "Month";
                        group.Converter = _monthNameConverter;
                        query = query.GroupBy(m => m.Month).SelectMany(e => e);

                        break;

                    case 2:
                        group.Member = "Date";
                        group.Converter = _dateCultureConverter;
                        query = query.GroupBy(m => m.Date).SelectMany(e => e);

                        break;

                    case 3:
                        group.Member = "Account.Name";
                        query = query.GroupBy(m => m.Account.Name).SelectMany(e => e);

                        break;

                    case 4:
                        group.Member = "MovementType.Name";
                        query = query.GroupBy(m => m.MovementType.Name).SelectMany(e => e);

                        break;

                    case 5:
                        group.Member = "MovementCategory.Name";
                        query = query.GroupBy(m => m.MovementCategory.Name).SelectMany(e => e);

                        break;

                    case 6:
                        group.Member = "MovementSubcategory.Name";
                        group.Converter = _subcategoryNameConverter;
                        query = query.GroupBy(m => m.MovementSubcategory == null ?
                            string.Empty : m.MovementSubcategory.Name).SelectMany(e => e);

                        break;

                    case 7:
                        group.Member = "Entity.Name";
                        group.Converter = _entityNameConverter;
                        query = query.GroupBy(m => m.Entity == null ?
                            string.Empty : m.Entity.Name).SelectMany(e => e);

                        break;

                    case 8:
                        group.Member = "Person.Name";
                        group.Converter = _personNameConverter;
                        query = query.GroupBy(m => m.Person == null ?
                            string.Empty : m.Person.Name).SelectMany(e => e);

                        break;

                    case 9:
                        group.Member = "Quantity";
                        query = query.GroupBy(m => m.Quantity).SelectMany(e => e);

                        break;

                    case 10:
                        group.Member = "Remarks";
                        query = query.GroupBy(m => m.Remarks).SelectMany(e => e);

                        break;

                    case 11:
                        group.Member = "Pending";
                        query = query.GroupBy(m => m.Pending).SelectMany(e => e);

                        break;
                }
            }

            return query;
        }

        private void SaveDbChanges()
        {
            try
            {
                _ctx.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                DiscardDbChanges();

                SqlException innerException = null;
                Exception tmp = ex;

                while (innerException == null && tmp != null)
                {
                    if (tmp != null)
                    {
                        innerException = tmp.InnerException as SqlException;
                        tmp = tmp.InnerException;
                    }
                }

                if (innerException != null && innerException.Number == 2601)
                    MessageBox.Show(TranslationSource.Instance["FieldMustBeUniqueText"],
                        TranslationSource.Instance["MessageBoxAttentionText"]);
                else
                    throw;
            }
        }

        private void DiscardDbChanges()
        {
            var changedEntries = _ctx.ChangeTracker.Entries()
                .Where(x => x.State != EntityState.Unchanged).ToList();

            foreach (var entry in changedEntries.Where(x => x.State == EntityState.Modified))
                entry.State = EntityState.Unchanged;

            foreach (var entry in changedEntries.Where(x => x.State == EntityState.Added))
                entry.State = EntityState.Detached;

            foreach (var entry in changedEntries.Where(x => x.State == EntityState.Deleted))
                entry.State = EntityState.Unchanged;

            // Tell the UI to refresh the data
            if (_movements != null)
                _movements.ForceOnCollectionChanged();
        }

        private void InitializeMovementsDataRefreshTimer()
        {
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 800);
        }

        private void RequestMovementsDataRefresh()
        {
            if (dispatcherTimer.IsEnabled)
                dispatcherTimer.Stop();

            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (dispatcherTimer.IsEnabled)
                dispatcherTimer.Stop();

            LoadMovementSetData();
        }

        #endregion

        #region Filtering

        private void LoadFilteringControlsData()
        {
            fromDatePickerFilter.SelectedDate = DateTime.Today.Subtract(_month);
            toDatePickerFilter.SelectedDate = DateTime.Today;

            Tools.SetSelectorData(accountListBoxFilter, _accounts, "Name", "Id");
            Tools.SetSelectorData(typeListBoxFilter, _ctx.MovementTypeSet, "Name", "Id");
            Tools.SetSelectorData(categoryListBoxFilter, _categories, "Name", "Id");
            Tools.SetSelectorData(entityListBoxFilter, _entities, "Name", "Id");
            Tools.SetSelectorData(personListBoxFilter, _persons, "Name", "Id");
        }

        private void SetSubcategoryListBoxFilterData()
        {
            List<int> categoryIds = new List<int>();

            foreach (MovementCategory category in categoryListBoxFilter.SelectedItems)
                categoryIds.Add(category.Id);

            IQueryable<MovementSubcategory> query = _ctx.MovementSubcategorySet
                .Where(s => categoryIds.Contains(s.MovementCategoryId)).OrderBy(o => o.Name);

            MovementSubcategoryCollection subcategories = new MovementSubcategoryCollection(query, _ctx);
            subcategoryListBoxFilter.DisplayMemberPath = "Name";
            subcategoryListBoxFilter.SelectedValuePath = "Id";
            subcategoryListBoxFilter.DataContext = subcategories;
            subcategoryListBoxFilter.ItemsSource = subcategories;
        }

        private void fromDatePickerFilter_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void toDatePickerFilter_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void accountListBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void typeListBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void categoryListBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetSubcategoryListBoxFilterData();
            RequestMovementsDataRefresh();
        }

        private void subcategoryListBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void entityListBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void personListBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void remarksTextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        private void pendingComboBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestMovementsDataRefresh();
        }

        #endregion

        #region Sorting and grouping

        private void LoadSortingAndGroupingControlsData()
        {
            _loadingComboBoxItems = true;

            List<string> sortingGroupingFields = new List<string>
            {
                TranslationSource.Instance["Year"],
                TranslationSource.Instance["Month"],
                TranslationSource.Instance["Date"],
                TranslationSource.Instance["Account"],
                TranslationSource.Instance["Type"],
                TranslationSource.Instance["Category"],
                TranslationSource.Instance["Subcategory"],
                TranslationSource.Instance["Entity"],
                TranslationSource.Instance["Person"],
                TranslationSource.Instance["Quantity"],
                TranslationSource.Instance["Remarks"],
                TranslationSource.Instance["Pending"]
            };

            int selectedIndex;

            if (sort1ComboBox.HasItems)
                selectedIndex = sort1ComboBox.SelectedIndex;
            else
                selectedIndex = 2; // Sort by date as default

            sort1ComboBox.ItemsSource = sortingGroupingFields;
            sort1ComboBox.SelectedIndex = selectedIndex;

            selectedIndex = sort2ComboBox.SelectedIndex;
            sort2ComboBox.ItemsSource = sortingGroupingFields;
            sort2ComboBox.SelectedIndex = selectedIndex;

            selectedIndex = sort3ComboBox.SelectedIndex;
            sort3ComboBox.ItemsSource = sortingGroupingFields;
            sort3ComboBox.SelectedIndex = selectedIndex;

            selectedIndex = group1ComboBox.SelectedIndex;
            group1ComboBox.ItemsSource = sortingGroupingFields;
            group1ComboBox.SelectedIndex = selectedIndex;

            selectedIndex = group2ComboBox.SelectedIndex;
            group2ComboBox.ItemsSource = sortingGroupingFields;
            group2ComboBox.SelectedIndex = selectedIndex;

            selectedIndex = group3ComboBox.SelectedIndex;
            group3ComboBox.ItemsSource = sortingGroupingFields;
            group3ComboBox.SelectedIndex = selectedIndex;

            List<string> kinds = new List<string> { TranslationSource.Instance["Ascending"],
                                                    TranslationSource.Instance["Descending"] };

            if (sort1KindComboBox.HasItems)
                selectedIndex = sort1KindComboBox.SelectedIndex; // Sort kind descending as default for the first field
            else
                selectedIndex = 1;

            sort1KindComboBox.ItemsSource = kinds;
            sort1KindComboBox.SelectedIndex = selectedIndex;

            if (sort2KindComboBox.HasItems)
                selectedIndex = sort2KindComboBox.SelectedIndex;
            else
                selectedIndex = 0; //Sort king ascending as default for the second field

            sort2KindComboBox.ItemsSource = kinds;
            sort2KindComboBox.SelectedIndex = selectedIndex;

            if (sort3KindComboBox.HasItems)
                selectedIndex = sort3KindComboBox.SelectedIndex;
            else
                selectedIndex = 0; //Sort kind ascending as default for the third field

            sort3KindComboBox.ItemsSource = kinds;
            sort3KindComboBox.SelectedIndex = selectedIndex;

            _loadingComboBoxItems = false;
        }

        private void sort1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void sort2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void sort3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void sort1KindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void sort2KindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void sort3KindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void group1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void group2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void group3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
                RequestMovementsDataRefresh();
        }

        private void dataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var binding = (e.Column as DataGridBoundColumn).Binding as Binding;

            _dataRefreshEnabled = false;

            sort1ComboBox.SelectedIndex = -1;
            sort2ComboBox.SelectedIndex = -1;
            sort3ComboBox.SelectedIndex = -1;

            sort1KindComboBox.SelectedIndex = 0;
            sort2KindComboBox.SelectedIndex = 0;
            sort3KindComboBox.SelectedIndex = 0;

            switch (binding.Path.Path)
            {
                case "Date":
                    sort1ComboBox.SelectedIndex = 2;
                    break;

                case "Account.Name":
                    sort1ComboBox.SelectedIndex = 3;
                    break;

                case "MovementType.Name":
                    sort1ComboBox.SelectedIndex = 4;
                    break;

                case "MovementCategory.Name":
                    sort1ComboBox.SelectedIndex = 5;
                    break;

                case "MovementSubcategory.Name":
                    sort1ComboBox.SelectedIndex = 6;
                    break;

                case "Entity.Name":
                    sort1ComboBox.SelectedIndex = 7;
                    break;

                case "Person.Name":
                    sort1ComboBox.SelectedIndex = 8;
                    break;

                case "Quantity":
                    sort1ComboBox.SelectedIndex = 9;
                    break;

                case "Remarks":
                    sort1ComboBox.SelectedIndex = 10;
                    break;

                case "Pending":
                    sort1ComboBox.SelectedIndex = 11;
                    break;
            }

            // After this event call SortDirection property 
            // will reflect the opposite of the current value
            if (e.Column.SortDirection == System.ComponentModel.ListSortDirection.Ascending)
                sort1KindComboBox.SelectedIndex = 1;

            _dataRefreshEnabled = true;
        }

        #endregion

        #region Editing

        private void LoadFieldsData()
        {
            datePicker.SelectedDate = DateTime.Today;

            Tools.SetSelectorData(accountComboBox, _accounts, "Id");

            Tools.SetSelectorData(typeComboBox, _ctx.MovementTypeSet, "Name", "Id");
            if (typeComboBox.HasItems)
                typeComboBox.SelectedIndex = 0;

            Tools.SetSelectorData(categoryComboBox, _categories, "Id");
            Tools.SetSelectorData(entityComboBox, _entities, "Id");
            Tools.SetSelectorData(personComboBox, _persons, "Id");
        }

        private void SetSubcategoryComboBoxData()
        {
            MovementCategory category = categoryComboBox.SelectedItem as MovementCategory;
            IQueryable<MovementSubcategory> query;

            if (category == null)
            {
                query = Enumerable.Empty<MovementSubcategory>().AsQueryable();
            }
            else
            {
                query = _ctx.MovementSubcategorySet.AsQueryable();
                query = query.Where(s => s.MovementCategoryId == category.Id).OrderBy(o => o.Name);
            }

            _editableSubcategories = new MovementSubcategoryCollection(query, _ctx);
            subcategoryComboBox.SelectedValuePath = "Id";
            subcategoryComboBox.DataContext = _editableSubcategories;
            subcategoryComboBox.ItemsSource = _editableSubcategories;
        }

        private void dataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            Movement m = dataGrid.CurrentItem as Movement;

            if (m != null)
            {
                _validateOnValueChanged = false;

                datePicker.SelectedDate = m.Date;
                accountComboBox.SelectedValue = m.AccountId;
                typeComboBox.SelectedValue = m.MovementTypeId;
                categoryComboBox.SelectedValue = m.MovementCategoryId;
                subcategoryComboBox.SelectedValue = m.MovementSubcategoryId;
                entityComboBox.SelectedValue = m.EntityId;
                personComboBox.SelectedValue = m.PersonId;
                quantityTextBox.Text = m.Quantity.ToString();
                remarksTextBox.Text = m.Remarks;
                pendingComboBox.SelectedIndex = m.Pending == false ? 0 : 1;

                _validateOnValueChanged = true;
            }

            ValidateAllFields();
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAllFields())
            {
                Movement m = new Movement();
                CreateFieldsNewData();
                SetFieldsDataToMovement(m);
                _movements.Add(m);
                SaveDbChanges();
                ClearAllFieldValidations();
                dataGrid.ScrollIntoView(m);
                dataGrid.SelectedItem = m;
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["FieldsValidationFailedText"],
                    TranslationSource.Instance["MessageBoxAttentionText)"]);
            }
        }

        private void modifyButton_Click(object sender, RoutedEventArgs e)
        {
            Movement m = dataGrid.SelectedItem as Movement;

            if (m != null)
            {
                if (ValidateAllFields())
                {
                    CreateFieldsNewData();
                    SetFieldsDataToMovement(m);
                    SaveDbChanges();
                    dataGrid.Items.Refresh();
                    ClearAllFieldValidations();
                }
                else
                {
                    MessageBox.Show(TranslationSource.Instance["FieldsValidationFailedText"],
                        TranslationSource.Instance["MessageBoxAttentionText"]);
                }
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["CannotUpdateNoRowSelectedText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
            }
        }

        private void removeButton_Click(object sender, RoutedEventArgs e)
        {
            Movement m = dataGrid.SelectedItem as Movement;

            if (m != null)
            {
                if (MessageBox.Show(TranslationSource.Instance["DeleteRecordQuestionText"],
                    TranslationSource.Instance["MessageBoxAttentionText"],
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _movements.Remove(m);
                    SaveDbChanges();
                }
            }
        }

        private void CreateFieldsNewData()
        {
            if (accountComboBox.SelectedValue == null && !string.IsNullOrEmpty(accountComboBox.Text))
            {
                Account a = new Account();
                a.Name = accountComboBox.Text;
                _accounts.Add(a);
                SaveDbChanges();
                accountComboBox.SelectedValue = a.Id;
            }

            if (categoryComboBox.SelectedValue == null && !string.IsNullOrEmpty(categoryComboBox.Text))
            {
                MovementCategory mc = new MovementCategory();
                mc.Name = categoryComboBox.Text;
                _categories.Add(mc);
                SaveDbChanges();
                categoryComboBox.SelectedValue = mc.Id;
            }

            if (subcategoryComboBox.SelectedValue == null && !string.IsNullOrEmpty(subcategoryComboBox.Text))
            {
                MovementSubcategory ms = new MovementSubcategory();
                ms.Name = subcategoryComboBox.Text;
                ms.MovementCategoryId = (int)categoryComboBox.SelectedValue;
                _editableSubcategories.Add(ms);
                SaveDbChanges();
                subcategoryComboBox.SelectedValue = ms.Id;
                SetSubcategoryListBoxFilterData();
            }

            if (entityComboBox.SelectedValue == null && !string.IsNullOrEmpty(entityComboBox.Text))
            {
                Entity e = new Entity();
                e.Name = entityComboBox.Text;
                _entities.Add(e);
                SaveDbChanges();
                entityComboBox.SelectedValue = e.Id;
            }

            if (personComboBox.SelectedValue == null && !string.IsNullOrEmpty(personComboBox.Text))
            {
                Person p = new Person();
                p.Name = personComboBox.Text;
                _persons.Add(p);
                SaveDbChanges();
                personComboBox.SelectedValue = p.Id;
            }
        }

        private void SetFieldsDataToMovement(Movement m)
        {
            m.Date = datePicker.SelectedDate.Value;
            m.AccountId = (int)accountComboBox.SelectedValue;
            m.MovementTypeId = (int)typeComboBox.SelectedValue;
            m.MovementCategoryId = (int)categoryComboBox.SelectedValue;
            m.MovementSubcategoryId = (int?)subcategoryComboBox.SelectedValue;
            m.EntityId = (int?)entityComboBox.SelectedValue;
            m.PersonId = (int?)personComboBox.SelectedValue;

            decimal quantity;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            quantity = decimal.Parse(quantityTextBox.Text.Replace(",", "."), culture);

            m.Quantity = quantity;

            m.Remarks = remarksTextBox.Text;
            m.Pending = pendingComboBox.SelectedIndex == 0 ? false : true;
        }

        private bool ValidateField(DatePicker datePicker, ContentControl visualWarning, bool required)
        {
            bool returnValue = true;
            Ellipse visualWarningEllipse = (Ellipse)visualWarning.Template.FindName("ValidationEllipse", visualWarning);

            if (required && !datePicker.SelectedDate.HasValue)
            {
                if (visualWarningEllipse != null)
                    visualWarningEllipse.Fill = Brushes.Tomato;

                visualWarning.ToolTip = TranslationSource.Instance["FieldRequiredToolTipText"];
                visualWarning.Visibility = Visibility.Visible;
                returnValue = false;
            }
            else
            {
                visualWarning.Visibility = Visibility.Hidden;
            }

            return returnValue;
        }

        private bool ValidateField(ComboBox combo, ContentControl visualWarning, bool editable, bool required)
        {
            bool returnValue = true;
            Ellipse visualWarningEllipse = (Ellipse)visualWarning.Template.FindName("ValidationEllipse", visualWarning);

            if (required && combo.SelectedItem == null
                && ((editable && string.IsNullOrEmpty(combo.Text)) || !editable))
            {
                if (visualWarningEllipse != null)
                    visualWarningEllipse.Fill = Brushes.Tomato;

                visualWarning.ToolTip = TranslationSource.Instance["FieldRequiredToolTipText"];
                visualWarning.Visibility = Visibility.Visible;
                returnValue = false;
            }
            else if (editable && combo.SelectedItem == null && !string.IsNullOrEmpty(combo.Text))
            {
                if (visualWarningEllipse != null)
                    visualWarningEllipse.Fill = Brushes.YellowGreen;

                visualWarning.ToolTip = TranslationSource.Instance["NewDataToolTipText"];
                visualWarning.Visibility = Visibility.Visible;
            }
            else
            {
                visualWarning.Visibility = Visibility.Hidden;
            }

            return returnValue;
        }

        private bool ValidateField(TextBox text, ContentControl visualWarning, bool required, bool checkDecimal)
        {
            decimal test;
            bool returnValue = true;
            Ellipse visualWarningEllipse = (Ellipse)visualWarning.Template.FindName("ValidationEllipse", visualWarning);

            if (required && string.IsNullOrEmpty(text.Text))
            {
                if (visualWarningEllipse != null)
                    visualWarningEllipse.Fill = Brushes.Tomato;

                visualWarning.ToolTip = TranslationSource.Instance["FieldRequiredToolTipText"];
                visualWarning.Visibility = Visibility.Visible;
                returnValue = false;
            }
            else if (checkDecimal && !decimal.TryParse(text.Text, out test))
            {
                if (visualWarningEllipse != null)
                    visualWarningEllipse.Fill = Brushes.Tomato;

                visualWarning.ToolTip = TranslationSource.Instance["FieldNotNumericToolTipText"];
                visualWarning.Visibility = Visibility.Visible;
                returnValue = false;
            }
            else
            {
                visualWarning.Visibility = Visibility.Hidden;
            }

            return returnValue;
        }

        private bool ValidateDateField()
        {
            return ValidateField(datePicker, dateWarning, true);
        }

        private bool ValidateAccountField()
        {
            return ValidateField(accountComboBox, accountWarning, true, true);
        }

        private bool ValidateTypeField()
        {
            return ValidateField(typeComboBox, typeWarning, false, true);
        }

        private bool ValidateCategoryField()
        {
            return ValidateField(categoryComboBox, categoryWarning, true, true);
        }

        private bool ValidateSubcategoryField()
        {
            return ValidateField(subcategoryComboBox, subcategoryWarning, true, false);
        }

        private bool ValidateEntityField()
        {
            return ValidateField(entityComboBox, entityWarning, true, false);
        }

        private bool ValidatePersonField()
        {
            return ValidateField(personComboBox, personWarning, true, false);
        }

        private bool ValidateQuantityField()
        {
            return ValidateField(quantityTextBox, quantityWarning, true, true);
        }

        private bool ValidatePendingField()
        {
            return ValidateField(pendingComboBox, pendingWarning, false, true);
        }

        private bool ValidateAllFields()
        {
            bool returnValue = true;

            returnValue &= ValidateDateField();
            returnValue &= ValidateAccountField();
            returnValue &= ValidateTypeField();
            returnValue &= ValidateCategoryField();
            returnValue &= ValidateSubcategoryField();
            returnValue &= ValidateEntityField();
            returnValue &= ValidatePersonField();
            returnValue &= ValidateQuantityField();
            returnValue &= ValidatePendingField();

            return returnValue;
        }

        private void ClearAllFieldValidations()
        {
            dateWarning.Visibility = Visibility.Hidden;
            accountWarning.Visibility = Visibility.Hidden;
            typeWarning.Visibility = Visibility.Hidden;
            categoryWarning.Visibility = Visibility.Hidden;
            subcategoryWarning.Visibility = Visibility.Hidden;
            entityWarning.Visibility = Visibility.Hidden;
            personWarning.Visibility = Visibility.Hidden;
            quantityWarning.Visibility = Visibility.Hidden;
            pendingWarning.Visibility = Visibility.Hidden;
        }

        private void UserDeleteAccount(object sender)
        {
            Account a = (Account)(sender as ContentControl).DataContext;

            if (a.MovementSet.Count == 0)
            {
                if (MessageBox.Show(TranslationSource.Instance["DeleteRecordQuestionText"],
                    TranslationSource.Instance["MessageBoxAttentionText"],
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _ctx.AccountSet.Remove(a);
                    SaveDbChanges();
                }
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["CannotDeleteRelatedMovementsText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
            }
        }

        private void UserDeleteCategory(object sender)
        {
            MovementCategory c = (MovementCategory)(sender as ContentControl).DataContext;

            if (c.MovementSet.Count == 0)
            {
                if (MessageBox.Show(TranslationSource.Instance["DeleteRecordQuestionText"],
                    TranslationSource.Instance["MessageBoxAttentionText"],
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _ctx.MovementSubcategorySet.RemoveRange(c.MovementSubcategorySet);
                    _categories.Remove(c);
                    SaveDbChanges();
                }
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["CannotDeleteRelatedMovementsText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
            }
        }

        private void UserDeleteSubcategory(object sender)
        {
            MovementSubcategory s = (MovementSubcategory)(sender as ContentControl).DataContext;

            if (s.MovementSet.Count == 0)
            {
                if (MessageBox.Show(TranslationSource.Instance["DeleteRecordQuestionText"],
                    TranslationSource.Instance["MessageBoxAttentionText"],
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _editableSubcategories.Remove(s);
                    SaveDbChanges();
                    SetSubcategoryListBoxFilterData();
                }
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["CannotDeleteRelatedMovementsText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
            }
        }

        private void UserDeleteEntity(object sender)
        {
            Entity n = (Entity)(sender as ContentControl).DataContext;

            if (n.MovementSet.Count == 0)
            {
                if (MessageBox.Show(TranslationSource.Instance["DeleteRecordQuestionText"],
                    TranslationSource.Instance["MessageBoxAttentionText"],
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _entities.Remove(n);
                    SaveDbChanges();
                }
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["CannotDeleteRelatedMovementsText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
            }
        }

        private void UserDeletePerson(object sender)
        {
            Person p = (Person)(sender as ContentControl).DataContext;

            if (p.MovementSet.Count == 0)
            {
                if (MessageBox.Show(TranslationSource.Instance["DeleteRecordQuestionText"],
                    TranslationSource.Instance["MessageBoxAttentionText"],
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _persons.Remove(p);
                    SaveDbChanges();
                }
            }
            else
            {
                MessageBox.Show(TranslationSource.Instance["CannotDeleteRelatedMovementsText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
            }
        }

        private void deleteAccountButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UserDeleteAccount(sender);
        }

        private void deleteCategoryButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UserDeleteCategory(sender);
        }

        private void deleteSubcategoryButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UserDeleteSubcategory(sender);
        }

        private void deleteEntityButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UserDeleteEntity(sender);
        }

        private void deletePersonButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UserDeletePerson(sender);
        }

        private void deleteAccountButton_StylusDown(object sender, StylusDownEventArgs e)
        {
            UserDeleteAccount(sender);
        }

        private void deleteCategoryButton_StylusDown(object sender, StylusDownEventArgs e)
        {
            UserDeleteCategory(sender);
        }

        private void deleteSubcategoryButton_StylusDown(object sender, StylusDownEventArgs e)
        {
            UserDeleteSubcategory(sender);
        }

        private void deleteEntityButton_StylusDown(object sender, StylusDownEventArgs e)
        {
            UserDeleteEntity(sender);
        }

        private void deletePersonButton_StylusDown(object sender, StylusDownEventArgs e)
        {
            UserDeletePerson(sender);
        }

        private void accountComboBox_DropDownClosed(object sender, EventArgs e)
        {
            UpdateAccountText();
        }

        private void categoryComboBox_DropDownClosed(object sender, EventArgs e)
        {
            UpdateCategoryText();
        }

        private void subcategoryComboBox_DropDownClosed(object sender, EventArgs e)
        {
            UpdateSubcategoryText();
        }

        private void entityComboBox_DropDownClosed(object sender, EventArgs e)
        {
            UpdateEntityText();
        }

        private void personComboBox_DropDownClosed(object sender, EventArgs e)
        {
            UpdatePersonText();
        }

        private void accountComboBox_DropDownOpened(object sender, EventArgs e)
        {
            AlignComboBoxDropDownPopupRight(accountComboBox);
        }

        private void categoryComboBox_DropDownOpened(object sender, EventArgs e)
        {
            AlignComboBoxDropDownPopupRight(categoryComboBox);
        }

        private void subcategoryComboBox_DropDownOpened(object sender, EventArgs e)
        {
            AlignComboBoxDropDownPopupRight(subcategoryComboBox);
        }

        private void entityComboBox_DropDownOpened(object sender, EventArgs e)
        {
            AlignComboBoxDropDownPopupRight(entityComboBox);
        }

        private void personComboBox_DropDownOpened(object sender, EventArgs e)
        {
            AlignComboBoxDropDownPopupRight(personComboBox);
        }

        private void AlignComboBoxDropDownPopupRight(ComboBox combo)
        {
            Popup popup = (Popup)combo.Template.FindName("PART_Popup", combo);

            if (popup != null)
            {
                popup.Placement = PlacementMode.Custom;
                popup.CustomPopupPlacementCallback = placePopup;
            }
        }

        private CustomPopupPlacement[] placePopup(Size popupSize, Size targetSize, Point offset)
        {
            CustomPopupPlacement[] placements = new[] { new CustomPopupPlacement() };
            placements[0].Point = new Point(targetSize.Width - popupSize.Width, targetSize.Height);

            return placements;
        }

        private void accountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidateAccountField();
        }

        private void typeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidateTypeField();
        }

        private void categoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidateCategoryField();

            SetSubcategoryComboBoxData();
        }

        private void subcategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidateSubcategoryField();
        }

        private void entityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidateEntityField();
        }

        private void personComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidatePersonField();
        }

        private void quantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            Tools.EnsureProperTextBoxDecimalFormat(tb, e.Changes);

            if (!_validateOnValueChanged)
                ValidateQuantityField();
        }

        private void quantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(DecimalRegex);

            if (regex.IsMatch(e.Text))
                e.Handled = true;
        }

        private void quantityTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                Regex regex = new Regex(DecimalRegex);

                if (regex.IsMatch((string)e.DataObject.GetData(typeof(string))))
                    e.CancelCommand();
            }
            else
                e.CancelCommand();
        }

        private void pendingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_validateOnValueChanged)
                ValidatePendingField();
        }

        private void datePicker_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateDateField();
        }

        private void accountComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateAccountText();
            ValidateAccountField();
        }

        private void typeComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateTypeField();
        }

        private void categoryComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateCategoryText();
            ValidateCategoryField();
        }

        private void subcategoryComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateSubcategoryText();
            ValidateSubcategoryField();
        }

        private void entityComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateEntityText();
            ValidateEntityField();
        }

        private void personComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePersonText();
            ValidatePersonField();
        }

        private void quantityTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateQuantityField();
        }

        private void pendingComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidatePendingField();
        }

        private void SaveEditableControlChanges(Control control)
        {
            SaveDbChanges();
            control.BindingGroup.CancelEdit();
        }

        private void TemplateEditableTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveEditableControlChanges((Control)sender);
        }

        private void TaxPercentageTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveEditableControlChanges((Control)sender);
            _movements.ForceOnCollectionChanged();
        }

        private void TaxPercentageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            Tools.EnsureProperTextBoxDecimalFormat(tb, e.Changes);
        }

        private void TaxPercentageTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(DecimalRegex);

            if (regex.IsMatch(e.Text))
                e.Handled = true;
        }

        private void TaxPercentageTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                Regex regex = new Regex(DecimalRegex);

                if (regex.IsMatch((string)e.DataObject.GetData(typeof(string))))
                    e.CancelCommand();
            }
            else
                e.CancelCommand();
        }

        private void expandGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            _movements.IsExpanded = true;
        }

        private void collapseGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            _movements.IsExpanded = false;
        }

        private void copyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            dataGrid.SelectAllCells();
            dataGrid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            ApplicationCommands.Copy.Execute(null, dataGrid);
            dataGrid.UnselectAllCells();
        }

        #endregion

        #region Editing (combo complements)

        private void AssignComboComplementDelegates()
        {
            DependencyPropertyDescriptor dp = DependencyPropertyDescriptor.FromProperty(
                                                ComboBox.TextProperty, typeof(ComboBox));

            accountComboBox.Loaded += delegate
            {
                dp.AddValueChanged(accountComboBox, (object a, EventArgs b) =>
                {
                    AssignCorrectAccountValues();
                });
            };

            categoryComboBox.Loaded += delegate
            {
                dp.AddValueChanged(categoryComboBox, (object a, EventArgs b) =>
                {
                    AssignCorrectCategoryValues();
                });
            };

            subcategoryComboBox.Loaded += delegate
            {
                dp.AddValueChanged(subcategoryComboBox, (object a, EventArgs b) =>
                {
                    AssignCorrectSubcategoryValues();
                });
            };

            entityComboBox.Loaded += delegate
            {
                dp.AddValueChanged(entityComboBox, (object a, EventArgs b) =>
                {
                    AssignCorrectEntityValues();
                });
            };

            personComboBox.Loaded += delegate
            {
                dp.AddValueChanged(personComboBox, (object a, EventArgs b) =>
                {
                    AssignCorrectPersonValues();
                });
            };
        }

        private void AssignCorrectAccountValues()
        {
            if (accountComboBox.SelectedValue != null)
            {
                IQueryable<Account> query = (IQueryable<Account>)accountComboBox
                    .Items.SourceCollection.AsQueryable();

                query = query.Where(z => z.Name.Equals(accountComboBox.Text.Trim()));
                List<Account> list = query.ToList();

                if (list.Count == 0)
                {
                    accountComboBox.SelectedValue = null;
                }
                else
                {
                    Account a = list[0];
                    if ((int?)accountComboBox.SelectedValue != a.Id)
                        accountComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void UpdateAccountText()
        {
            if (accountComboBox.SelectedItem != null)
            {
                Account a = accountComboBox.SelectedItem as Account;

                if (!accountComboBox.Text.Equals(a.Name))
                {
                    accountComboBox.Text = a.Name;
                    accountComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void AssignCorrectCategoryValues()
        {
            if (categoryComboBox.SelectedValue != null)
            {
                IQueryable<MovementCategory> query = (IQueryable<MovementCategory>)categoryComboBox
                    .Items.SourceCollection.AsQueryable();

                query = query.Where(z => z.Name.Equals(categoryComboBox.Text.Trim()));
                List<MovementCategory> list = query.ToList();

                if (list.Count == 0)
                {
                    categoryComboBox.SelectedValue = null;
                }
                else
                {
                    MovementCategory a = list[0];
                    if ((int?)categoryComboBox.SelectedValue != a.Id)
                        categoryComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void UpdateCategoryText()
        {
            if (categoryComboBox.SelectedItem != null)
            {
                MovementCategory a = categoryComboBox.SelectedItem as MovementCategory;

                if (!categoryComboBox.Text.Equals(a.Name))
                {
                    categoryComboBox.Text = a.Name;
                    categoryComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void AssignCorrectSubcategoryValues()
        {
            if (subcategoryComboBox.SelectedValue != null)
            {
                IQueryable<MovementSubcategory> query = (IQueryable<MovementSubcategory>)subcategoryComboBox
                    .Items.SourceCollection.AsQueryable();

                query = query.Where(z => z.Name.Equals(subcategoryComboBox.Text.Trim()));
                List<MovementSubcategory> list = query.ToList();

                if (list.Count == 0)
                {
                    subcategoryComboBox.SelectedValue = null;
                }
                else
                {
                    MovementSubcategory a = list[0];
                    if ((int?)subcategoryComboBox.SelectedValue != a.Id)
                        subcategoryComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void UpdateSubcategoryText()
        {
            if (subcategoryComboBox.SelectedItem != null)
            {
                MovementSubcategory a = subcategoryComboBox.SelectedItem as MovementSubcategory;

                if (!subcategoryComboBox.Text.Equals(a.Name))
                {
                    subcategoryComboBox.Text = a.Name;
                    subcategoryComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void AssignCorrectEntityValues()
        {
            if (entityComboBox.SelectedValue != null)
            {
                IQueryable<Entity> query = (IQueryable<Entity>)entityComboBox
                    .Items.SourceCollection.AsQueryable();

                query = query.Where(z => z.Name.Equals(entityComboBox.Text.Trim()));
                List<Entity> list = query.ToList();

                if (list.Count == 0)
                {
                    entityComboBox.SelectedValue = null;
                }
                else
                {
                    Entity a = list[0];
                    if ((int?)entityComboBox.SelectedValue != a.Id)
                        entityComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void UpdateEntityText()
        {
            if (entityComboBox.SelectedItem != null)
            {
                Entity a = entityComboBox.SelectedItem as Entity;

                if (!entityComboBox.Text.Equals(a.Name))
                {
                    entityComboBox.Text = a.Name;
                    entityComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void AssignCorrectPersonValues()
        {
            if (personComboBox.SelectedValue != null)
            {
                IQueryable<Person> query = (IQueryable<Person>)personComboBox
                    .Items.SourceCollection.AsQueryable();

                query = query.Where(z => z.Name.Equals(personComboBox.Text.Trim()));
                List<Person> list = query.ToList();

                if (list.Count == 0)
                {
                    personComboBox.SelectedValue = null;
                }
                else
                {
                    Person a = list[0];
                    if ((int?)personComboBox.SelectedValue != a.Id)
                        personComboBox.SelectedValue = a.Id;
                }
            }
        }

        private void UpdatePersonText()
        {
            if (personComboBox.SelectedItem != null)
            {
                Person a = personComboBox.SelectedItem as Person;

                if (!personComboBox.Text.Equals(a.Name))
                {
                    personComboBox.Text = a.Name;
                    personComboBox.SelectedValue = a.Id;
                }
            }
        }

        #endregion

        #region Other actions

        private void loadDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(databaseTextBox.Text))
            {
                MessageBox.Show(TranslationSource.Instance["CannotLoadDatabaseNameRequiredText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
                return;
            }
            else if (string.Equals(databaseTextBox.Text.ToLower(), _ctx.Database.Connection.Database.ToLower()))
            {
                MessageBox.Show(TranslationSource.Instance["DatabaseAlreadyLoadedText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
                return;
            }
            else
            {
                Tools.ChangeDatabaseConnectionStringConfig(databaseTextBox.Text);

                MainWindow newWindow = new MainWindow();
                newWindow.Show();
                Close();
            }
        }

        private void deleteDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(databaseTextBox.Text))
            {
                MessageBox.Show(TranslationSource.Instance["CannotLoadDatabaseNameRequiredText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
                return;
            }
            else if (string.Equals(databaseTextBox.Text.ToLower(), _ctx.Database.Connection.Database.ToLower()))
            {
                MessageBox.Show(TranslationSource.Instance["CannotDeleteDatabaseInUseText"],
                    TranslationSource.Instance["MessageBoxAttentionText"]);
                return;
            }
            else
            {
                string[] filePaths = Directory.GetFiles(_databasePath);

                if (Tools.AreDatabaseFilesOnPathArray(databaseTextBox.Text, filePaths))
                {
                    if (MessageBox.Show(TranslationSource.Instance["DeleteDatabaseQuestionText"],
                        TranslationSource.Instance["MessageBoxAttentionText"],
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            MovementsModel ctxToDelete = new MovementsModel(
                                Tools.GetNewConnectionString(databaseTextBox.Text));

                            ctxToDelete.Database.Delete();
                            ctxToDelete.Dispose();
                        }
                        catch
                        {
                        }
                        finally
                        {
                            Tools.DeleteDatabaseFilesFromPathArray(databaseTextBox.Text, filePaths);
                        }

                        MessageBox.Show(TranslationSource.Instance["DeleteDatabaseCompleteText"],
                            TranslationSource.Instance["MessageBoxAttentionText"]);
                    }
                }
                else
                {
                    MessageBox.Show(TranslationSource.Instance["CannotDeleteDatabaseNotFoundText"],
                        TranslationSource.Instance["MessageBoxAttentionText"]);
                }
            }
        }

        private void viewExistingFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(_databasePath);
        }

        private void languageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingComboBoxItems)
            {
                Tools.ChangeCultureConfig(((CultureSelectionData)languageComboBox.SelectedItem).Culture);

                SetAppTitle();

                LoadStaticControls();

                LoadSortingAndGroupingControlsData();

                // This forces the UI to refresh data to reflect
                // culture change on the grid
                if (_movements != null)
                    _movements.ForceOnCollectionChanged();
            }
        }

        private void aboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow newWindow = new AboutWindow();
            newWindow.Owner = this;
            newWindow.ShowDialog();
        }

        #endregion

        #region Misc

        private void DisableMouseWheelInput()
        {
            pendingComboBoxFilter.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);

            sort1ComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            sort2ComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            sort3ComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);

            group1ComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            group2ComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            group3ComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);

            accountComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            typeComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            categoryComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            subcategoryComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            entityComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            personComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
            pendingComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);

            languageComboBox.PreviewMouseWheel += new MouseWheelEventHandler(ComboBox_PreviewMouseWheel);
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _ctx.Dispose();
        }

        #endregion
    }
}