﻿// -------------------------------------------------------------------------------------------
// <copyright file="RepositoryPresenter.cs" company="MapWindow OSS Team - www.mapwindow.org">
//  MapWindow OSS Team - 2015
// </copyright>
// -------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Windows.Forms;
using MW5.Api.Concrete;
using MW5.Api.Enums;
using MW5.Api.Static;
using MW5.Data.Enums;
using MW5.Data.Repository;
using MW5.Data.Views;
using MW5.Plugins.Interfaces;
using MW5.Plugins.Model;
using MW5.Plugins.Mvp;
using MW5.Plugins.Services;
using MW5.Shared;
using MW5.Tiles.Views;

namespace MW5.Plugins.Repository.Views
{
    public class RepositoryPresenter : CommandDispatcher<RepositoryDockPanel, RepositoryCommand>, IDockPanelPresenter
    {
        private readonly IAppContext _context;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILayerService _layerService;
        private readonly IRepository _repository;
        private readonly RepositoryDockPanel _view;

        public RepositoryPresenter(
            IAppContext context,
            RepositoryDockPanel view,
            IFileDialogService fileDialogService,
            ILayerService layerService,
            IRepository repository)
            : base(view)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (fileDialogService == null) throw new ArgumentNullException("fileDialogService");
            if (layerService == null) throw new ArgumentNullException("layerService");
            if (repository == null) throw new ArgumentNullException("repository");

            _context = context;
            _fileDialogService = fileDialogService;
            _layerService = layerService;
            _repository = repository;
            _view = view;

            _view.ItemDoubleClicked += ViewItemDoubleClicked;
            _view.TreeViewKeyDown += OnTreeViewKeyDown;
        }

        public Control GetInternalObject()
        {
            return _view;
        }

        public override void RunCommand(RepositoryCommand command)
        {
            switch (command)
            {
                case RepositoryCommand.AddTms:
                    AddTmsProvider();
                    break;
                case RepositoryCommand.RemoveTms:
                    RemoveTmsProvider();
                    break;
                case RepositoryCommand.Properties:
                    EditTmsProvider();
                    break;
                case RepositoryCommand.AddFolder:
                    _repository.AddFolderLink();
                    break;
                case RepositoryCommand.RemoveFolder:
                    RemoveFolder();
                    break;
                case RepositoryCommand.AddToMap:
                    AddToMap();
                    break;
                case RepositoryCommand.RemoveFile:
                    RemoveFile();
                    break;
                case RepositoryCommand.OpenLocation:
                    OpenFileLocation();
                    break;
                case RepositoryCommand.GdalInfo:
                    ShowGdalInfo();
                    break;
                case RepositoryCommand.AddFolderToMap:
                    AddFolderToMap();
                    break;
                case RepositoryCommand.Refresh:
                    RefreshItem();
                    break;
                case RepositoryCommand.AddConnection:
                    AddConnection();
                    break;
                case RepositoryCommand.RemoveConnection:
                    RemoveConnection();
                    break;
                case RepositoryCommand.RemoveLayer:
                    RemoveDatabaseLayer();
                    break;
            }

            _context.View.Update();
        }

        private void EditTmsProvider()
        {
            var item = GetSelectedItem<ITmsItem>();
            if (item == null) return;

            var provider = item.Provider;

            // it's currently displayed, so let's update it
            bool needUpdate = _context.Map.Tiles.ProviderId == item.Provider.Id;

            if (_context.Container.Run<TmsProviderPresenter, TmsProvider>(provider))
            {
                _repository.TmsProviders.Update(provider);
                if (needUpdate)
                {
                    AddTmsProviderToMap(true);
                }
            }
        }

        private void AddTmsProvider()
        {
            var item = GetSelectedItem<IRepositoryItem>();
            if (item != null && item.Type == RepositoryItemType.TmsRoot)
            {
                var provider = new TmsProvider();
                if (_context.Container.Run<TmsProviderPresenter, TmsProvider>(provider))
                {
                    _repository.TmsProviders.Add(provider);
                }
            }
        }

        private void RemoveTmsProvider()
        {
            var item = GetSelectedItem<ITmsItem>();
            if (item == null) return;

            string msg = string.Format("Do you want to remove TMS provider: {0}?", item.Provider.Name);
            
            if (MessageService.Current.Ask(msg))
            {
                _repository.TmsProviders.Remove(item.Provider);
            }
        }

        private void AddConnection()
        {
            var item = GetSelectedItem<IRepositoryItem>() as IServerItem;
            if (item != null)
            {
                _repository.AddConnectionWithPrompt(item.DatabaseType);
            }
        }

        private void AddFolderToMap()
        {
            var folder = GetSelectedItem<IFolderItem>();
            if (folder != null)
            {
                _layerService.BeginBatch();

                try
                {
                    foreach (var item in folder.SubItems)
                    {
                        var file = item as IFileItem;
                        if (file != null)
                        {
                            _layerService.AddLayersFromFilename(file.Filename);
                        }
                    }
                }
                finally
                {
                    _layerService.EndBatch();
                }
            }
        }

        private void AddToMap()
        {
            var item = GetSelectedItem<IRepositoryItem>();

            if (item is ILayerItem)
            {
                AddLayerToMap(item as ILayerItem);
                return;
            }

            if (item is ITmsItem)
            {
                AddTmsProviderToMap(false);
            }
        }

        private void AddTmsProviderToMap(bool update)
        {
            var tms = GetSelectedItem<ITmsItem>();
            if (tms == null) return;

            var provider = tms.Provider;

            var providers = _context.Map.Tiles.Providers;
            providers.Clear(false);
            if (!providers.AddCustom(provider.Id, provider.Name, provider.Url, provider.Projection, provider.MinZoom, provider.MaxZoom))
            {
                MessageService.Current.Info("Failed to add custom TMS provider.");    
                return;
            }

            UpdateTmsBounds(provider, update);

            _context.Map.Tiles.GridLinesVisible = true;   // temporary
            _context.Map.Tiles.ProviderId = provider.Id;

            _context.Map.Redraw(RedrawType.Minimal, true);

            if (!update)
            {
                MessageService.Current.Info("TMS provider was added to the map as a base layer: " + Environment.NewLine +
                                            provider.Name);
            }
        }

        private void UpdateTmsBounds(TmsProvider provider, bool update)
        {
            var mapProvider = _context.Map.Tiles.Providers.FirstOrDefault(p => p.Id == provider.Id);
            if (mapProvider != null)
            {
                mapProvider.GeographicBounds = provider.UseBounds ? provider.Bounds : TmsProvider.DefaultBounds;

                if (!update && !_context.Map.Layers.Any())
                {
                    _context.Map.SetGeographicExtents(mapProvider.GeographicBounds);
                }
            }
        }

        private void AddLayerToMap(ILayerItem layer)
        {
            if (layer.AddedToMap)
            {
                _layerService.RemoveLayer(layer.Identity);
            }
            else
            {
                if (_layerService.AddLayerIdentity(layer.Identity))
                {
                    int handle = _layerService.LastLayerHandle;
                    _context.Map.ZoomToLayer(handle);
                }
            }
        }

        private T GetSelectedItem<T>() where T : class, IRepositoryItem
        {
            var item = _view.Tree.SelectedItem as T;
            if (item == null)
            {
                throw new InvalidCastException("Invalid type of the selected item.");
            }

            return item;
        }

        private void OnTreeViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelectedItem();
            }
        }

        private void RemoveSelectedItem()
        {
            var item = _view.Tree.SelectedItem;
            if (item != null)
            {
                if (item is IFileItem)
                {
                    RunCommand(RepositoryCommand.RemoveFile);
                }
                else if (item is IFolderItem)
                {
                    RunCommand(RepositoryCommand.RemoveFolder);
                }
                else if (item is IDatabaseItem)
                {
                    RunCommand(RepositoryCommand.RemoveConnection);
                }
                else if (item is IDatabaseLayerItem)
                {
                    RunCommand(RepositoryCommand.RemoveLayer);
                }
            }
        }

        private void OpenFileLocation()
        {
            var item = GetSelectedItem<IRepositoryItem>();
            string path = string.Empty;
            var folder = item as IFolderItem;
            if (folder != null)
            {
                path = folder.GetPath();
            }

            var vector = item as IFileItem;
            if (vector != null)
            {
                path = vector.Filename;
            }

            PathHelper.OpenFolderWithExplorer(path);
        }

        private void RefreshItem()
        {
            var item = GetSelectedItem<IRepositoryItem>();
            if (item is IFolderItem || item is IDatabaseItem)
            {
                item.Refresh();
            }
        }

        private void RemoveConnection()
        {
            var item = GetSelectedItem<IDatabaseItem>();
            if (item != null && MessageService.Current.Ask("Do you want to remove connection?"))
            {
                _repository.RemoveConnection(item.Connection, false);
            }
        }

        private void RemoveDatabaseLayer()
        {
            var layer = GetSelectedItem<IDatabaseLayerItem>();
            if (layer != null)
            {
                var db = layer.Parent as IDatabaseItem;
                if (db != null)
                {
                    var ds = new VectorDatasource();
                    if (ds.Open(db.Connection.ConnectionString))
                    {
                        int layerIndex = ds.LayerIndexByName(layer.Name);
                        if (MessageService.Current.Ask("Do you want to remove database layer: " + layer.Name + "?"))
                        {
                            if (!ds.DeleteLayer(layerIndex))
                            {
                                MessageService.Current.Warn("Failed to remove layer.");
                            }

                            db.Refresh();
                        }
                    }
                }
            }
        }

        private void RemoveFile()
        {
            var item = GetSelectedItem<IFileItem>();

            if (item == null)
            {
                MessageService.Current.Info("No filename is selected.");
                return;
            }

            if (_context.Layers.Select(l => l.Identity).Contains(item.Identity))
            {
                MessageService.Current.Info("Can't remove datasource currently opened by the program.");
                return;
            }

            if (
                MessageService.Current.Ask("Do you want to remove the datasource: " + Environment.NewLine +
                                           item.Filename + "?"))
            {
                try
                {
                    var folder = item.Folder;
                    GeoSource.Remove(item.Filename);

                    folder.Refresh();
                }
                catch (Exception ex)
                {
                    MessageService.Current.Warn("Failed to remove file: " + ex.Message);
                }
            }
        }

        private void RemoveFolder()
        {
            var item = View.Tree.SelectedItem as IFolderItem;
            if (item != null && item.Root)
            {
                _repository.RemoveFolderLink(item.GetPath(), false);
            }
        }

        private void ShowGdalInfo()
        {
            var item = GetSelectedItem<IFileItem>();
            if (item != null)
            {
                var model = new GdalInfoModel(item);
                _context.Container.Run<GdalInfoPresenter, GdalInfoModel>(model);
            }
        }

        private void ViewItemDoubleClicked(object sender, RepositoryEventArgs e)
        {
            if (e.Item is IFileItem || e.Item is IDatabaseLayerItem || e.Item is ITmsItem)
            {
                RunCommand(RepositoryCommand.AddToMap);
            }
        }
    }
}