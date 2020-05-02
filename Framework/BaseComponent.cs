﻿namespace Macabre2D.Framework {

    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// The base class for all components.
    /// </summary>
    [DataContract]
    public abstract class BaseComponent : NotifyPropertyChanged, IBaseComponent, IDisposable {
        protected bool _disposedValue;

        [DataMember]
        private readonly ObservableCollection<BaseComponent> _children = new ObservableCollection<BaseComponent>();

        private readonly List<Func<BaseComponent, bool>> _resolveChildActions = new List<Func<BaseComponent, bool>>();
        private readonly ResettableLazy<Matrix> _transformMatrix;

        private int _drawOrder;
        private bool _isEnabled = true;
        private bool _isTransformUpToDate;
        private bool _isVisible = true;
        private Layers _layers = Layers.Layer01;
        private Vector2 _localPosition;
        private Vector2 _localScale = Vector2.One;
        private string _name;

        [DataMember]
        private BaseComponent _parent;

        private Transform _transform = new Transform();

        private int _updateOrder;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseComponent"/> class.
        /// </summary>
        protected BaseComponent() {
            this.PropertyChanged += this.Self_PropertyChanged;
            this.ParentChanged += this.BaseComponent_ParentChanged;
            this._children.CollectionChanged += this.Children_CollectionChanged;
            this._transformMatrix = new ResettableLazy<Matrix>(this.GetMatrix);
        }

        /// <inheritdoc/>
        public event EventHandler<BaseComponent> ParentChanged;

        /// <inheritdoc/>
        public IReadOnlyCollection<BaseComponent> Children {
            get {
                return this._children;
            }
        }

        /// <inheritdoc/>
        [DataMember(Name = "Draw Order")]
        public int DrawOrder {
            get {
                return this._drawOrder;
            }

            set {
                this.Set(ref this._drawOrder, value, true);
            }
        }

        /// <inheritdoc/>
        [DataMember]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <inheritdoc/>
        [DataMember(Name = "Enable")]
        public bool IsEnabled {
            get {
                return this._isEnabled && (this.Parent == null || this.Parent.IsEnabled);
            }
            set {
                this.Set(ref this._isEnabled, value, this.Parent == null || this.Parent.IsEnabled);
            }
        }

        /// <summary>
        /// Gets a value indicating whether or not this instance has been initialized.
        /// </summary>
        /// <value>A value indicating whether or not this instance has been initialized.</value>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        [DataMember(Name = "Visible")]
        public bool IsVisible {
            get {
                return this.IsEnabled && this._isVisible;
            }
            set {
                if (value != this._isVisible) {
                    this._isVisible = value;

                    if (this.IsEnabled) {
                        this.RaisePropertyChanged(true);
                    }
                }
            }
        }

        /// <inheritdoc/>
        [DataMember]
        public Layers Layers {
            get {
                return this._layers;
            }

            set {
                this.Set(ref this._layers, value);
            }
        }

        /// <summary>
        /// Gets or sets the local position.
        /// </summary>
        /// <value>The local position.</value>
        [DataMember(Name = "Local Position")]
        public Vector2 LocalPosition {
            get {
                return this._localPosition;
            }
            set {
                if (this.Set(ref this._localPosition, value)) {
                    this.HandleMatrixOrTransformChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the local scale.
        /// </summary>
        /// <value>The local scale.</value>
        [DataMember(Name = "Local Scale")]
        public Vector2 LocalScale {
            get {
                return this._localScale;
            }
            set {
                if (this.Set(ref this._localScale, value)) {
                    this.HandleMatrixOrTransformChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember]
        public string Name {
            get {
                return this._name;
            }

            set {
                this.Set(ref this._name, value);
            }
        }

        /// <inheritdoc/>
        public BaseComponent Parent {
            get {
                return this._parent;
            }

            set {
                var valueId = value?.Id;
                if (this._parent?.Id != valueId && this.Id != valueId) {
                    var originalParent = this._parent;
                    this._parent = value;

                    if (this._parent != null) {
                        var wasInitialized = this.IsInitialized;
                        this._parent.AddChild(this);

                        if (this.IsInitialized == wasInitialized && this.IsInitialized) {
                            this._parent.PropertyChanged += this.Parent_PropertyChanged;
                        }

                        if (originalParent != null) {
                            originalParent.RemoveChild(this);
                            originalParent.PropertyChanged -= this.Parent_PropertyChanged;
                        }
                    }
                    else if (originalParent != null) {
                        originalParent.RemoveChild(this);
                        originalParent.PropertyChanged -= this.Parent_PropertyChanged;
                    }

                    this.ParentChanged.SafeInvoke(this, this._parent);
                }
            }
        }

        /// <inheritdoc/>
        public int SessionId { get; internal set; }

        /// <summary>
        /// Gets the transform matrix.
        /// </summary>
        /// <value>The transform matrix.</value>
        public Matrix TransformMatrix {
            get {
                return this._transformMatrix.Value;
            }
        }

        /// <inheritdoc/>
        [DataMember(Name = "Update Order")]
        public int UpdateOrder {
            get {
                return this._updateOrder;
            }

            set {
                this.Set(ref this._updateOrder, value, true);
            }
        }

        /// <inheritdoc/>
        public Transform WorldTransform {
            get {
                if (!this._isTransformUpToDate) {
                    this._transform = this.TransformMatrix.DecomposeWithoutRotation2D();
                    this._isTransformUpToDate = true;
                }

                return this._transform;
            }
        }

        /// <summary>
        /// Gets the scene.
        /// </summary>
        /// <value>The scene.</value>
        protected IScene Scene { get; private set; } = EmptyScene.Instance;

        /// <summary>
        /// Adds the child.
        /// </summary>
        /// <param name="child">Child.</param>
        public bool AddChild(BaseComponent child) {
            var result = false;
            if (child != null && child.Id != this.Id && !this._children.Any(x => x.Id == child.Id) && !this.IsDescendentOf(child)) {
                this._children.Add(child);
                child.Parent = this;
                result = true;

                if (this.IsInitialized) {
                    child.Initialize(this.Scene);
                }
            }

            return result;
        }

        /// <summary>
        /// Adds a new child component to this component. The new component will not be completely
        /// added until next update.
        /// </summary>
        /// <typeparam name="T">A class of type <see cref="BaseComponent"/>.</typeparam>
        /// <returns>The added component.</returns>
        public T AddChild<T>() where T : BaseComponent, new() {
            var component = new T { IsEnabled = true };
            this.AddChild(component);
            return component;
        }

        /// <summary>
        /// Adds the child.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">
        /// Exception gets hit if you don't pass in a type that inherits from <see cref="BaseComponent"/>.
        /// </exception>
        public BaseComponent AddChild(Type type) {
            if (typeof(BaseComponent).IsAssignableFrom(type)) {
                var component = Activator.CreateInstance(type) as BaseComponent;
                component.IsEnabled = true;
                this.AddChild(component);
                return component;
            }
            else {
                throw new NotSupportedException($"{type.FullName} is does not inherit from {typeof(BaseComponent).FullName}");
            }
        }

        /// <inheritdoc/>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finds the component with the specified name in this component's children.
        /// </summary>
        /// <returns>The component in children.</returns>
        /// <param name="name">Name.</param>
        public BaseComponent FindComponentInChildren(string name) {
            foreach (var child in this.Children) {
                if (string.Equals(name, child.Name)) {
                    return child;
                }

                var found = child.FindComponentInChildren(name);

                if (found != null) {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all children, including children of children and so on.
        /// </summary>
        /// <returns>All child components.</returns>
        public IEnumerable<BaseComponent> GetAllChildren() {
            var children = new List<BaseComponent>();

            foreach (var component in this.Children) {
                children.Add(component);
                children.AddRange(component.GetAllChildren());
            }

            return children;
        }

        /// <summary>
        /// Gets the child that is the specified type.
        /// </summary>
        /// <returns>The component.</returns>
        /// <typeparam name="T">A class of type <see cref="BaseComponent"/>.</typeparam>
        public T GetChild<T>() where T : BaseComponent {
            return (T)this.Children.FirstOrDefault(x => x.GetType() == typeof(T));
        }

        /// <summary>
        /// Gets a child of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        /// A <see cref="BaseComponent"/> of the specified type in children, or null if it doesn't exist.
        /// </returns>
        public BaseComponent GetChild(Type type) {
            return this.Children.FirstOrDefault(x => x.GetType() == type || type.IsAssignableFrom(x.GetType()));
        }

        /// <summary>
        /// Gets a child of the specified type and with the specified name.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        /// A <see cref="BaseComponent"/> of the specified type in children, or null if it doesn't exist.
        /// </returns>
        public BaseComponent GetChild(Type type, string name) {
            return this.Children.FirstOrDefault(x => x.Name == name && (x.GetType() == type || type.IsAssignableFrom(x.GetType())));
        }

        /// <summary>
        /// Gets a child of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the component.</typeparam>
        /// <returns>A child of the specified type.</returns>
        public T GetChildOfType<T>() {
            return this._children.OfType<T>().First();
        }

        /// <summary>
        /// Gets the children that are the specified type.
        /// </summary>
        /// <typeparam name="T">A type that a component could be.</typeparam>
        /// <returns>All components that match the type.</returns>
        public IEnumerable<T> GetChildren<T>() {
            return this.Children.OfType<T>();
        }

        /// <summary>
        /// Gets the children of a specified type.
        /// </summary>
        /// <typeparam name="T">The type of the component.</typeparam>
        /// <returns>All children that match the type.</returns>
        public IEnumerable<T> GetChildrenOfType<T>() {
            return this._children.OfType<T>();
        }

        /// <summary>
        /// Gets the component as this component's parent, or that component's parent, or that
        /// component's parent, and so on.
        /// </summary>
        /// <typeparam name="T">A class of type <see cref="BaseComponent"/>.</typeparam>
        /// <returns>A component of the specified type in this component's ancestors.</returns>
        public T GetComponentFromParent<T>() where T : BaseComponent {
            if (this.Parent != null) {
                if (this.Parent is T generic) {
                    return generic;
                }
                else {
                    return this.Parent.GetComponentFromParent<T>();
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a component of the specified type in this component's children.
        /// </summary>
        /// <typeparam name="T">The type of component.</typeparam>
        /// <param name="isShallowSearch">if set to <c>true</c> will only search one level deep.</param>
        /// <returns>The found component.</returns>
        public T GetComponentInChildren<T>(bool isShallowSearch) where T : BaseComponent {
            var component = (T)this._children.FirstOrDefault(x => x.GetType() == typeof(T));

            if (!isShallowSearch && component == null) {
                foreach (var child in this.Children) {
                    component = child.GetComponentInChildren<T>(isShallowSearch);

                    if (component != null) {
                        break;
                    }
                }
            }

            return component;
        }

        /// <summary>
        /// Gets the components of the specified type in this object and in this object's children.
        /// </summary>
        /// <typeparam name="T">A component.</typeparam>
        /// <returns>All components of the specified type in this object and this object's children.</returns>
        public List<T> GetComponentsInChildren<T>() {
            var components = new List<T>();
            components.AddRange(this._children.OfType<T>());

            foreach (var child in this.Children) {
                components.AddRange(child.GetComponentsInChildren<T>());
            }

            return components;
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(float rotationAngle) {
            var worldTransform = this.WorldTransform;
            var matrix =
                Matrix.CreateScale(worldTransform.Scale.X, worldTransform.Scale.Y, 1f) *
                Matrix.CreateRotationZ(rotationAngle) *
                Matrix.CreateTranslation(worldTransform.Position.X, worldTransform.Position.Y, 0f);

            return matrix.ToTransform();
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(Vector2 originOffset) {
            var matrix = Matrix.CreateTranslation(originOffset.X, originOffset.Y, 0f) * this.TransformMatrix;
            return matrix.ToTransform();
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(Vector2 originOffset, float rotationAngle) {
            var worldTransform = this.WorldTransform;

            var matrix =
                Matrix.CreateTranslation(originOffset.X, originOffset.Y, 0f) *
                Matrix.CreateScale(worldTransform.Scale.X, worldTransform.Scale.Y, 1f) *
                Matrix.CreateRotationZ(rotationAngle) *
                Matrix.CreateTranslation(worldTransform.Position.X, worldTransform.Position.Y, 0f);

            return matrix.ToTransform();
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(Vector2 originOffset, Vector2 overrideScale, float rotationAngle) {
            var worldTransform = this.WorldTransform;

            var matrix =
                Matrix.CreateScale(overrideScale.X, overrideScale.Y, 1f) *
                Matrix.CreateTranslation(originOffset.X, originOffset.Y, 0f) *
                Matrix.CreateRotationZ(rotationAngle) *
                Matrix.CreateTranslation(worldTransform.Position.X, worldTransform.Position.Y, 0f);

            return matrix.ToTransform();
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(Vector2 originOffset, Vector2 overrideScale) {
            var worldTransform = this.WorldTransform;

            var matrix =
                Matrix.CreateScale(overrideScale.X, overrideScale.Y, 1f) *
                Matrix.CreateTranslation(originOffset.X, originOffset.Y, 0f) *
                Matrix.CreateTranslation(worldTransform.Position.X, worldTransform.Position.Y, 0f);

            return matrix.ToTransform();
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(TileGrid grid, Point gridTileLocation) {
            var position = new Vector2(gridTileLocation.X * grid.TileSize.X, gridTileLocation.Y * grid.TileSize.Y) + grid.Offset;
            return this.GetWorldTransform(position);
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(TileGrid grid, Point gridTileLocation, Vector2 offset) {
            var position = new Vector2(gridTileLocation.X * grid.TileSize.X, gridTileLocation.Y * grid.TileSize.Y) + grid.Offset + offset;
            return this.GetWorldTransform(position);
        }

        /// <inheritdoc/>
        public Transform GetWorldTransform(TileGrid grid, Point gridTileLocation, Vector2 offset, float rotationAngle) {
            var position = new Vector2(gridTileLocation.X * grid.TileSize.X, gridTileLocation.Y * grid.TileSize.Y) + grid.Offset + offset;
            return this.GetWorldTransform(position, rotationAngle);
        }

        /// <summary>
        /// Initialize this instance.
        /// </summary>
        public void Initialize(IScene scene) {
            if (!this.IsInitialized || this.Scene == null || this.Scene != scene) {
                this.Scene = scene;
                this.ResolveChildren();

                try {
                    this.Initialize();
                }
                finally {
                    this.IsInitialized = true;
                }

                if (this._parent != null) {
                    this._parent.PropertyChanged += this.Parent_PropertyChanged;
                }

                foreach (var child in this._children) {
                    child.Initialize(scene);
                }
            }
        }

        /// <summary>
        /// Determines whether this instance is an ancestor of the specified component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <returns>
        /// <c>true</c> if this instance is an ancestor of the specified component; otherwise, <c>false</c>.
        /// </returns>
        public bool IsAncestorOf(BaseComponent component) {
            var result = false;
            if (component != null) {
                foreach (var child in this.Children) {
                    if (result) {
                        break;
                    }

                    result = child.Id == component.Id || child.IsAncestorOf(component);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether this instance is a descendent of the specified component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <returns>
        /// <c>true</c> if this instance is a descendent of the specified component; otherwise, <c>false</c>.
        /// </returns>
        public bool IsDescendentOf(BaseComponent component) {
            var result = false;

            if (component != null) {
                result = component.Id == this.Parent?.Id || (this.Parent?.IsDescendentOf(component) == true);
            }

            return result;
        }

        /// <inheritdoc/>
        public virtual void LoadContent() {
            foreach (var child in this._children) {
                child.LoadContent();
            }
        }

        /// <summary>
        /// Removes the child.
        /// </summary>
        /// <param name="child">Child.</param>
        public bool RemoveChild(BaseComponent child) {
            var result = false;
            if (child != null && this._children.Remove(child)) {
                child.Parent = null;
                result = true;
            }

            return result;
        }

        /// <inheritdoc/>
        public void SetWorldPosition(Vector2 position) {
            this.SetWorldTransform(position, this.WorldTransform.Scale);
        }

        /// <inheritdoc/>
        public void SetWorldScale(Vector2 scale) {
            this.SetWorldTransform(this.WorldTransform.Position, scale);
        }

        /// <inheritdoc/>
        public void SetWorldTransform(Vector2 position, Vector2 scale) {
            var matrix =
                Matrix.CreateScale(scale.X, scale.Y, 1f) *
                Matrix.CreateTranslation(position.X, position.Y, 0f);

            if (this.Parent != null) {
                matrix *= Matrix.Invert(this.Parent.TransformMatrix);
            }

            var localTransform = matrix.ToTransform();
            this._localPosition = localTransform.Position;
            this._localScale = localTransform.Scale;
            this.HandleMatrixOrTransformChanged();
        }

        /// <summary>
        /// Subscribes to the children changed event.
        /// </summary>
        /// <param name="handler">The handler.</param>
        public void SubscribeToChildrenChanged(NotifyCollectionChangedEventHandler handler) {
            this._children.CollectionChanged += handler;
        }

        /// <summary>
        /// Unsubscribes from the children changed event.
        /// </summary>
        /// <param name="handler">The handler.</param>
        public void UnsubscribeFromChildrenChanged(NotifyCollectionChangedEventHandler handler) {
            this._children.CollectionChanged -= handler;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release
        /// only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing) {
            if (!this._disposedValue) {
                if (disposing) {
                }

                foreach (var child in this._children) {
                    child.Dispose();
                }

                if (this._parent != null) {
                    this._parent.PropertyChanged -= this.Parent_PropertyChanged;
                }

                this.DisposePropertyChanged();
                this.ParentChanged = null;
                this._children.Clear();
                this._parent = null;
                this._resolveChildActions.Clear();
                this._children.CollectionChanged -= this.Children_CollectionChanged;
                this._disposedValue = true;
            }
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        protected abstract void Initialize();

        private void BaseComponent_ParentChanged(object sender, BaseComponent e) {
            this.HandleMatrixOrTransformChanged();
        }

        private void Children_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (this.IsInitialized && this._resolveChildActions.Any() && e.Action == NotifyCollectionChangedAction.Add && e.NewItems.OfType<BaseComponent>().FirstOrDefault() is BaseComponent newComponent) {
                var actions = this._resolveChildActions.ToList();
                foreach (var action in actions) {
                    if (action.Invoke(newComponent)) {
                        this._resolveChildActions.Remove(action);
                    }
                }
            }
        }

        private Matrix GetMatrix() {
            var transformMatrix =
                Matrix.CreateScale(this.LocalScale.X, this.LocalScale.Y, 1f) *
                Matrix.CreateTranslation(this.LocalPosition.X, this.LocalPosition.Y, 0f);

            if (this.Parent != null) {
                transformMatrix *= this.Parent.TransformMatrix;
            }

            return transformMatrix;
        }

        private void HandleMatrixOrTransformChanged() {
            this._transformMatrix.Reset();
            this._isTransformUpToDate = false;
            this.RaisePropertyChanged(true, nameof(this.WorldTransform));
        }

        private void Parent_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(this.WorldTransform)) {
                this.HandleMatrixOrTransformChanged();
            }
            else if (e.PropertyName == nameof(this.IsEnabled)) {
                if (this._isEnabled) {
                    this.RaisePropertyChanged(true, nameof(this.IsEnabled));
                }
            }
        }

        private void ResolveChildren() {
            var type = this.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
            var properties = type.GetProperties(flags);

            foreach (var property in properties) {
                if (property.CanWrite && property.PropertyType.IsSubclassOf(typeof(BaseComponent)) && property.GetSetMethod() != null) {
                    var attributes = property.GetCustomAttributes(typeof(ChildAttribute), true);

                    if (attributes.FirstOrDefault() is ChildAttribute childAttribute) {
                        BaseComponent component;

                        if (childAttribute.UseExisting) {
                            if (childAttribute.Name == null) {
                                component = this.GetChild(property.PropertyType);
                            }
                            else {
                                component = this.GetChild(property.PropertyType, childAttribute.Name);
                            }
                        }
                        else {
                            component = this.AddChild(property.PropertyType);
                        }

                        if (component != null) {
                            property.SetValue(this, component);
                        }
                        else {
                            this._resolveChildActions.Add(new Func<BaseComponent, bool>(newComponent => {
                                var result = false;
                                if (property.PropertyType.IsAssignableFrom(newComponent.GetType())) {
                                    property.SetValue(this, newComponent);
                                    result = true;
                                }

                                return result;
                            }));
                        }
                    }
                }
            }

            var fields = type.GetFields(flags);
            foreach (var field in fields) {
                if (field.FieldType.IsSubclassOf(typeof(BaseComponent))) {
                    var attributes = field.GetCustomAttributes(typeof(ChildAttribute), true);
                    if (attributes.FirstOrDefault() is ChildAttribute childAttribute) {
                        BaseComponent component;
                        if (childAttribute.Name == null) {
                            component = this.GetChild(field.FieldType);
                        }
                        else {
                            component = this.GetChild(field.FieldType, childAttribute.Name);
                        }

                        if (component != null) {
                            field.SetValue(this, component);
                        }
                        else {
                            this._resolveChildActions.Add(new Func<BaseComponent, bool>(newComponent => {
                                var result = false;
                                if (field.FieldType.IsAssignableFrom(newComponent.GetType())) {
                                    field.SetValue(this, newComponent);
                                    result = true;
                                }

                                return result;
                            }));
                        }
                    }
                }
            }
        }

        private void Self_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(this.IsEnabled) && this._isVisible) {
                this.RaisePropertyChanged(true, nameof(this.IsVisible));
            }
        }
    }
}