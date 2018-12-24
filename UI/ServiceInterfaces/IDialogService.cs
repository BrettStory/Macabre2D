﻿namespace Macabre2D.UI.ServiceInterfaces {

    using Macabre2D.Framework;
    using Macabre2D.UI.Models;
    using System;
    using System.Windows;

    public interface IDialogService {

        bool ShowCreateProjectDialog(out Project project, string initialDirectory = null);

        bool ShowFileBrowser(string filter, out string path, string initialDirectory = null);

        bool ShowFolderBrowser(out string path, string initialDirectory = null);

        SaveDiscardCancelResult ShowSaveDiscardCancelDialog();

        SceneAsset ShowSaveSceneWindow(Project project, Scene scene);

        Asset ShowSelectAssetDialog(Project project, AssetType assetMask, AssetType selectableAssetMask);

        (Type Type, string Name) ShowSelectTypeAndNameDialog(Type type);

        Type ShowSelectTypeDialog(Type type);

        MessageBoxResult ShowYesNoCancelMessageBox(string title, string message);

        bool ShowYesNoMessageBox(string title, string message);
    }
}