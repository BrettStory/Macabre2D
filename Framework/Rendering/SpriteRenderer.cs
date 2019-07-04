﻿namespace Macabre2D.Framework.Rendering {

    using Macabre2D.Framework.Extensions;
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// A component which will render a single sprite.
    /// </summary>
    /// <seealso cref="BaseComponent"/>
    /// <seealso cref="IDrawableComponent"/>
    /// <seealso cref="IDisposable"/>
    /// <seealso cref="BaseComponent"/>
    public sealed class SpriteRenderer : BaseComponent, IDrawableComponent, IAssetComponent<Sprite>, IRotatable {
        private readonly ResettableLazy<BoundingArea> _boundingArea;
        private readonly ResettableLazy<RotatableTransform> _rotatableTransform;
        private Vector2 _offset;
        private OffsetType _offsetType = OffsetType.Custom;
        private Sprite _sprite;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteRenderer"/> class.
        /// </summary>
        public SpriteRenderer() {
            this._boundingArea = new ResettableLazy<BoundingArea>(this.CreateBoundingArea);
            this._rotatableTransform = new ResettableLazy<RotatableTransform>(this.CreateRotatableTransform);
        }

        /// <inheritdoc/>
        public BoundingArea BoundingArea {
            get {
                return this._boundingArea.Value;
            }
        }

        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        /// <value>The color.</value>
        [DataMember]
        public Color Color { get; set; } = Color.White;

        /// <summary>
        /// Gets or sets the offset. If OFfsetType is anything other than Custom, this will be
        /// overridden when LoadContent(...) is called. This value is in pixels.
        /// </summary>
        /// <value>The offset.</value>
        [DataMember]
        public Vector2 Offset {
            get {
                return this._offset;
            }
            set {
                this.SetOffset(value);
                this._offsetType = OffsetType.Custom;
            }
        }

        /// <summary>
        /// Gets or sets the type of the offset.
        /// </summary>
        /// <value>The type of the offset.</value>
        [DataMember]
        public OffsetType OffsetType {
            get {
                return this._offsetType;
            }
            set {
                if (value != this._offsetType) {
                    this._offsetType = value;

                    if (this.IsInitialized) {
                        this.SetOffset();
                    }
                }
            }
        }

        /// <inheritdoc/>
        [DataMember]
        public Rotation Rotation { get; private set; } = new Rotation();

        /// <summary>
        /// Gets or sets the sprite.
        /// </summary>
        /// <value>The sprite.</value>
        [DataMember]
        public Sprite Sprite {
            get {
                return this._sprite;
            }
            set {
                if (this._sprite != value) {
                    this._sprite = value;
                    this.LoadContent();
                    this._boundingArea.Reset();

                    if (this.IsInitialized) {
                        this.SetOffset();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Draw(GameTime gameTime, BoundingArea viewBoundingArea) {
            MacabreGame.Instance.SpriteBatch.Draw(this.Sprite, this._rotatableTransform.Value, this.Color);
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetOwnedAssetIds() {
            return this.Sprite != null ? new[] { this.Sprite.Id } : new Guid[0];
        }

        /// <inheritdoc/>
        public bool HasAsset(Guid id) {
            return this._sprite?.Id == id;
        }

        /// <inheritdoc/>
        public override void LoadContent() {
            if (this.Sprite != null && this.Sprite.ContentId != Guid.Empty && this.Scene.IsInitialized) {
                try {
                    this.Sprite.Texture = AssetManager.Instance.Load<Texture2D>(this.Sprite.ContentId);
                }
                catch {
                    this.Sprite.SetErrorTexture(MacabreGame.Instance.SpriteBatch);
                }
            }

            base.LoadContent();
        }

        /// <inheritdoc/>
        public void RefreshAsset(Sprite newInstance) {
            if (newInstance != null && this.Sprite?.Id == newInstance.Id) {
                this.Sprite = newInstance;
            }
        }

        /// <inheritdoc/>
        public bool RemoveAsset(Guid id) {
            var result = this.HasAsset(id);
            if (result) {
                this.Sprite = null;
            }

            return result;
        }

        /// <inheritdoc/>
        public bool TryGetAsset(Guid id, out Sprite asset) {
            var result = this.Sprite != null && this.Sprite.Id == id;
            asset = result ? this.Sprite : null;
            return result;
        }

        /// <inheritdoc/>
        protected override void Initialize() {
            this.SetOffset();
            this.TransformChanged += this.Self_TransformChanged;

            if (this.Rotation == null) {
                this.Rotation = new Rotation();
            }

            this.Rotation.AngleChanged += this.Self_TransformChanged;
        }

        private BoundingArea CreateBoundingArea() {
            BoundingArea result;
            if (this.Sprite != null) {
                var width = this.Sprite.Size.X * GameSettings.Instance.InversePixelsPerUnit;
                var height = this.Sprite.Size.Y * GameSettings.Instance.InversePixelsPerUnit;
                var offset = this.Offset * GameSettings.Instance.InversePixelsPerUnit;
                var rotationAngle = this.Rotation.Angle;

                var points = new List<Vector2> {
                    this.GetWorldTransform(offset, rotationAngle).Position,
                    this.GetWorldTransform(offset + new Vector2(width, 0f), rotationAngle).Position,
                    this.GetWorldTransform(offset + new Vector2(width, height), rotationAngle).Position,
                    this.GetWorldTransform(offset + new Vector2(0f, height), rotationAngle).Position
                };

                var minimumX = points.Min(x => x.X);
                var minimumY = points.Min(x => x.Y);
                var maximumX = points.Max(x => x.X);
                var maximumY = points.Max(x => x.Y);

                result = new BoundingArea(new Vector2(minimumX, minimumY), new Vector2(maximumX, maximumY));
            }
            else {
                result = new BoundingArea();
            }

            return result;
        }

        private RotatableTransform CreateRotatableTransform() {
            return this.GetWorldTransform(this.Offset * GameSettings.Instance.InversePixelsPerUnit, this.Rotation.Angle);
        }

        private void Self_TransformChanged(object sender, EventArgs e) {
            this._boundingArea.Reset();
            this._rotatableTransform.Reset();
        }

        private void SetOffset(Vector2 newOffset) {
            this._offset = newOffset;
            this._rotatableTransform.Reset();
            this._boundingArea.Reset();
        }

        private void SetOffset() {
            if (this.Sprite == null || this.Sprite.Texture == null || this.OffsetType == OffsetType.Custom) {
                return;
            }

            switch (this.OffsetType) {
                case OffsetType.Bottom:
                    this.SetOffset(new Vector2(-this.Sprite.Texture.Width * 0.5f, 0f));
                    break;

                case OffsetType.BottomLeft:
                    this.SetOffset(Vector2.Zero);
                    break;

                case OffsetType.BottomRight:
                    this.SetOffset(new Vector2(-this.Sprite.Texture.Width, 0f));
                    break;

                case OffsetType.Center:
                    this.SetOffset(new Vector2(-this.Sprite.Texture.Width * 0.5f, -this.Sprite.Texture.Height * 0.5f));
                    break;

                case OffsetType.Left:
                    this.SetOffset(new Vector2(0f, -this.Sprite.Texture.Height * 0.5f));
                    break;

                case OffsetType.Right:
                    this.SetOffset(new Vector2(-this.Sprite.Texture.Width, -this.Sprite.Texture.Height * 0.5f));
                    break;

                case OffsetType.Top:
                    this.SetOffset(new Vector2(-this.Sprite.Texture.Width * 0.5f, -this.Sprite.Texture.Height));
                    break;

                case OffsetType.TopLeft:
                    this.SetOffset(new Vector2(0f, -this.Sprite.Texture.Height));
                    break;

                case OffsetType.TopRight:
                    this.SetOffset(new Vector2(-this.Sprite.Texture.Width, -this.Sprite.Texture.Height));
                    break;
            }
        }
    }
}