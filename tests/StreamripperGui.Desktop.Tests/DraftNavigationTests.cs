namespace StreamripperGui.Desktop.Tests;

public sealed class DraftNavigationTests
{
    [Xunit.Fact]
    public async Task SwitchingEditorsProtectsDraftsAndSynchronizesStationAssignments()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "draft-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Infrastructure.JsonConfigurationStore(directory);
            var configuration = Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration();
            configuration["recording"]!["profiles"] = System.Text.Json.Nodes.JsonNode.Parse("""
                                                                                            [{"id":"one","name":"One","options":{}},{"id":"two","name":"Two","options":{}}]
                                                                                            """);
            configuration["metadataRuleSets"] = System.Text.Json.Nodes.JsonNode.Parse("""
                                                                                      [{"id":"one","name":"One","file":"rules/one.txt"},{"id":"two","name":"Two","file":"rules/two.txt"}]
                                                                                      """);
            store.SaveConfiguration(configuration);
            store.SaveStations(new Infrastructure.StationFile
            {
                Stations =
                [
                    new() { Id = "one", Name = "One", Url = "https://example.com/one", RecordingProfileId = "one", MetadataRuleSetId = "one" },
                    new() { Id = "two", Name = "Two", Url = "https://example.com/two", RecordingProfileId = "two", MetadataRuleSetId = "two" },
                    new() { Id = "plain", Name = "Plain", Url = "https://example.com/plain" }
                ]
            });
            var files = new Infrastructure.MetadataRuleFiles(directory);
            files.Write("rules/one.txt", Core.Models.MetadataRules.Template);
            files.Write("rules/two.txt", Core.Models.MetadataRules.Template);
            using var model = new ViewModels.MainWindowViewModel(store);
            var first = model.VisibleStations.Single(s => s.Id == "one");
            var second = model.VisibleStations.Single(s => s.Id == "two");
            var one = model.SharedProfiles.Single(p => p.Id == "one");
            var two = model.SharedProfiles.Single(p => p.Id == "two");
            var decision = ViewModels.UnsavedChangesChoice.Cancel;
            var prompts = 0;
            model.ConfirmUnsavedChanges = _ =>
            {
                prompts++;
                return Task.FromResult(decision);
            };

            model.SelectedTabIndex = 1;
            Xunit.Assert.Same(one, model.SelectedProfile);
            model.ProfileNameDraft = "Edited";
            Xunit.Assert.False(await model.SelectProfileAsync(two));
            Xunit.Assert.Same(one, model.SelectedProfile);
            Xunit.Assert.Equal("Edited", model.ProfileNameDraft);
            Xunit.Assert.False(await model.SelectStationAsync(second));
            Xunit.Assert.Same(first, model.SelectedStation);

            decision = ViewModels.UnsavedChangesChoice.Save;
            model.ProfileNameDraft = "";
            Xunit.Assert.False(await model.SelectProfileAsync(two));
            Xunit.Assert.Same(one, model.SelectedProfile);
            model.ProfileNameDraft = "Saved";
            Xunit.Assert.True(await model.SelectProfileAsync(two));
            Xunit.Assert.Equal("Saved", store.LoadConfiguration()["recording"]!["profiles"]![0]!["name"]!.GetValue<string>());

            model.ProfileNameDraft = "Discarded";
            decision = ViewModels.UnsavedChangesChoice.Discard;
            Xunit.Assert.True(await model.SelectStationAsync(first));
            // The same station does not replace an independently selected editor.
            Xunit.Assert.Same(two, model.SelectedProfile);
            Xunit.Assert.True(await model.SelectStationAsync(second));
            Xunit.Assert.Same(two, model.SelectedProfile);
            Xunit.Assert.True(await model.SelectStationAsync(first));
            Xunit.Assert.Same(one, model.SelectedProfile);

            model.SelectedTabIndex = 2;
            Xunit.Assert.Equal("one", model.Rules.Selected?.Id);
            var original = model.Rules.Text;
            model.Rules.Name = "";
            decision = ViewModels.UnsavedChangesChoice.Save;
            Xunit.Assert.False(await model.SelectStationAsync(second));
            Xunit.Assert.Same(first, model.SelectedStation);
            Xunit.Assert.Equal("one", model.Rules.Selected?.Id);
            Xunit.Assert.Empty(model.Rules.Name);
            model.Rules.Name = "One";
            model.Rules.Text = original + "# unsaved\n";
            decision = ViewModels.UnsavedChangesChoice.Cancel;
            Xunit.Assert.False(await model.SelectStationAsync(second));
            Xunit.Assert.Same(first, model.SelectedStation);
            Xunit.Assert.Equal(original + "# unsaved\n", model.Rules.Text);
            Xunit.Assert.False(await model.Rules.NewDraftAsync());
            decision = ViewModels.UnsavedChangesChoice.Discard;
            Xunit.Assert.True(await model.SelectStationAsync(second));
            Xunit.Assert.Equal("two", model.Rules.Selected?.Id);
            Xunit.Assert.Equal(original, files.Read("rules/one.txt"));
            Xunit.Assert.Equal(original, model.Rules.Text);

            Xunit.Assert.True(await model.Rules.NewDraftAsync());
            Xunit.Assert.True(model.Rules.IsDirty);
            decision = ViewModels.UnsavedChangesChoice.Cancel;
            Xunit.Assert.False(await model.Rules.SelectAsync(null));
            Xunit.Assert.True(model.Rules.IsDirty);
            decision = ViewModels.UnsavedChangesChoice.Discard;
            Xunit.Assert.True(await model.Rules.SelectAsync(null));
            Xunit.Assert.False(model.Rules.IsDirty);
            Xunit.Assert.False(model.Rules.CanEdit);
            Xunit.Assert.Empty(model.Rules.Text);
            var plain = model.VisibleStations.Single(s => s.Id == "plain");
            Xunit.Assert.True(await model.SelectStationAsync(plain));
            Xunit.Assert.Null(model.Rules.Selected);
            model.SelectedTabIndex = 1;
            Xunit.Assert.Null(model.SelectedProfile?.Id);
            Xunit.Assert.NotNull(model.SelectedProfile);
            Xunit.Assert.True(prompts >= 7);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}