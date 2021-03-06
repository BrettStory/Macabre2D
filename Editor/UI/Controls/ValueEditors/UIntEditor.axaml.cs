﻿namespace Macabresoft.Macabre2D.Editor.UI.Controls.ValueEditors {
    using System;
    using Avalonia.Markup.Xaml;

    public class UIntEditor : BaseNumericEditor<uint> {
        public UIntEditor() {
            this.InitializeComponent();
        }

        protected override uint ConvertValue(object calculatedValue) {
            return Convert.ToUInt32(calculatedValue);
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }
    }
}