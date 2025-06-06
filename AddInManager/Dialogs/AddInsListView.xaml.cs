﻿using System.IO;

namespace AddInManager
{
    internal partial class AddInsListView : System.Windows.Controls.UserControl
    {
        private class ListItem
        {
            public ListItem(AddInVersionInfo addin)
            {
                this.Addin = addin;
            }

            public string? CompanyName => Addin.CompanyName;
            public string? ProductName => Addin.IsVersioned ? Addin.ProductName : Path.GetFileNameWithoutExtension(Addin.Path ?? Addin.Uri?.LocalPath);
            public string? Version => Addin.Version?.ToString();

            public AddInVersionInfo Addin { get; }
        }

        public AddInsListView()
        {
            InitializeComponent();
        }

        public void Add(List<AddInVersionInfo> addins)
        {
            foreach (var i in addins.Select(i => new ListItem(i)).OrderBy(i => i.ProductName))
                addinsListView.Items.Add(i);
        }

        public List<AddInVersionInfo> GetSelectedAddins()
        {
            return addinsListView.SelectedItems.Cast<ListItem>().Select(i => i.Addin).ToList();
        }
    }
}
