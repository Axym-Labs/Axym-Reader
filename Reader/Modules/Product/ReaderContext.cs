﻿namespace Reader.Modules.Product;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using Newtonsoft.Json;
using Reader.Data.Product;
using Reader.Data.Storage;

public class ReaderContext
{
    public ReaderManager Manager { get; private set; }
    public ReaderConfigManager ConfigManager { get; private set; }

    private ReaderState _state = default!;
    public ReaderState State { get => _state; set => _state = value; }

    private ReaderConfig _config = default!;
    public ReaderConfig Config { get => _config; set => _config = value; }

    public MudTextField<string> stateTitle { get; set; } = new();
    public MudTextField<string> stateText { get; set; } = new();

    private SiteInteraction SiteInteraction { get; set; }

    public ReaderContext(SiteInteraction siteInteraction)
    {
        SiteInteraction = siteInteraction;
    }

    public async Task TriggerOnInitializedEvents()
    {
        // set default state and config for initialization of reader
        await SetState(ReaderState.GetDemo());
        await SetConfig(ReaderConfig.GetDefault());

        // init reader
        InitializeReader();
        // init config manager
        InitializeConfigManager();
    }

    public async Task TriggerAfterFirstRenderEvents()
    {
        if (State == null)
            throw new("State must be initialized");
        if (Manager == null)
            throw new("Manager must be initialized");

        Manager.jsInteropAllowed = true;

        await LoadConfig();

        // start reader if demo
        if (State.Title == ReaderState.GetDemo().Title)
            Manager.StartReadingTask();
    }

    private async Task LoadConfig()
    {
        string? loadedConfigStr = await SiteInteraction.JSRuntime.InvokeAsync<string?>("loadConfigurationStrIfExists");
        if (!string.IsNullOrEmpty(loadedConfigStr))
        {
            _config = JsonConvert.DeserializeObject<ReaderConfig>(loadedConfigStr!)!;
            InitializeConfigManager();

            // should work without this line
            await SetConfig(Config);
        }
    }

    public async Task SetState(ReaderState newState)
    {
        await HandleStateUpdated(newState);
        await SiteInteraction.HandleSiteStateChanged();
    }

    public async Task SetConfig(ReaderConfig newConfig)
    {
        _config = newConfig;

        // should work without these 2 statements
        if (ConfigManager != null)
            ConfigManager.Config = Config;
        if (Manager != null)
            Manager.Config = Config;

        await SiteInteraction.HandleSiteStateChanged();
    }

    private void InitializeReader()
    {
        if (State == null)
            throw new("State must be initialized");
        if (Config == null)
            throw new("Config must be initialized");

        Manager = new(ref _state, ref _config, SiteInteraction);
    }

    private void InitializeConfigManager()
    {
        if (Manager == null)
            throw new("Manager must be initialized");
        if (Config == null)
            throw new("Config must be initialized");

        ConfigManager = new(ref _config, SiteInteraction, Manager.SetupTextPieces);
    }

    public async Task HandleNewText()
    {
        await SetState(ReaderState.GetNew());
        await Manager.UpdateSavedState();
    }

    public async Task HandleStateUpdated(ReaderState newState)
    {
        if (Manager != null)
            Manager.StopReadingTask();
        State = newState;

        await stateTitle.SetText(State.Title);
        await stateText.SetText(State.Text);

        // should work without this line
        if (Manager != null)
        {
            Manager.State = newState;
            Manager.SetupTextPieces();
        }
        if (Manager != null)
            Manager.ClampPosition();

        await SiteInteraction.HandleSiteStateChanged();
    }

    public async Task HandlePasteTitle()
    {
        State.Title = await SiteInteraction.JSRuntime.InvokeAsync<string>("getClipboardContent");

        // should work without this line
        await HandleStateUpdated(State);

        await stateTitle.SetText(State.Title);
        await SiteInteraction.HandleSiteStateChanged();
    }

    public async Task HandlePasteText()
    {
        State.Text  = await SiteInteraction.JSRuntime.InvokeAsync<string>("getClipboardContent");

        // should work without this line
        await HandleStateUpdated(State);

        await stateText.SetText(State.Text);
        await SiteInteraction.HandleSiteStateChanged();
    }

    public async Task HandleFileUpload(IReadOnlyList<IBrowserFile> files)
    {
        string importedText = await FileHelper.ExtractFromBrowserFiles(files);

        State.Text = importedText;

        // should work without this line
        await SetState(State);

        await stateText.SetText(importedText);
    }

    public async Task HandleTextChanged(string Text)
    {
        State.Text = Text.Trim();
        if (State.Text == string.Empty)
        {
            State.Text = ProductStorage.DefaultNewText;

            // should work without this line
            await HandleStateUpdated(State);
        }
        Manager.SetupTextPieces();
        
        await Manager.UpdateSavedState();
    }

    public async Task HandleTitleChanged(string title)
    {
        State.Title = title;
        await Manager.RenameSavedState(State.Title, title);
    }
}
