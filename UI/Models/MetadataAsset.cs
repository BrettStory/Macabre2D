﻿namespace Macabre2D.UI.Models {

    using Macabre2D.Framework;
    using Macabre2D.UI.Common;
    using System.IO;

    public class MetadataAsset : Asset {

        public MetadataAsset(string fileName) : base(fileName) {
            this.PropertyChanged += this.Self_PropertyChanged;
        }

        public MetadataAsset() : this(string.Empty) {
        }

        public bool HasChanges {
            get;
            internal set;
        }

        public string MetadataFileName {
            get {
                return $"{this.Name}{FileHelper.MetaDataExtension}";
            }
        }

        public void Delete() {
            File.Delete(this.GetPath());
            File.Delete(this.GetMetadataPath());
        }

        public string GetMetadataPath() {
            return $"{this.GetPath()}{FileHelper.MetaDataExtension}";
        }

        public void Save(Serializer serializer) {
            if (this.HasChanges) {
                try {
                    this.SaveChanges(serializer);
                    serializer.Serialize(this, this.GetMetadataPath());
                }
                finally {
                    this.HasChanges = false;
                }
            }
        }

        protected virtual void SaveChanges(Serializer serializer) {
            return;
        }

        private void Self_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            this.HasChanges = true;
        }
    }
}