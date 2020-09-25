﻿namespace Macabresoft.MonoGame.Core {

    using Microsoft.Xna.Framework.Content.Pipeline;

    /// <summary>
    /// Content importer for <see cref="AssetManager" />.
    /// </summary>
    [ContentImporter(".m2dam", DefaultProcessor = nameof(AssetManagerProcessor), DisplayName = "Asset Manager Importer - Macabresoft.MonoGame.Core")]
    public sealed class AssetManagerImporter : JsonImporter {
    }
}