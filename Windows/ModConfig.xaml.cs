﻿using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;
using System.Windows.Shapes;
using System.Security.Cryptography.X509Certificates;


namespace AemulusModManager
{
    /// <summary>
    /// Interaction logic for ModConfig.xaml
    /// </summary>
    public partial class ModConfig : Window
    {
        public ConfigMetadata cfgmetadata;
        public string thumbnailPath;

        public ModConfig(ConfigMetadata mm)
        {
            InitializeComponent();
            if (mm != null)
            {
                cfgmetadata = mm;
                Title = $"Edit {mm.name} Configuration Options";
                Utilities.ParallelLogger.Log($"[DEBUG] Message 1: Within ModConfig, mm.modgame is set to {mm.modgame}.");
                Utilities.ParallelLogger.Log($"[DEBUG] Message 2: Within ModConfig, mm.modpath is set to {mm.modpath}.");
            }
        }
        private void OptionNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(OptionNameBox.Text))
                CreateButton.IsEnabled = true;
            else
                CreateButton.IsEnabled = false;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Look I'll be fully honest I'm not a great coder, whatever works works, I barely know what I'm doing so if this is bad feel free to correct me. Sincerely, Solt11.
            string path = $@"{System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Packages\{cfgmetadata.modgame}\{cfgmetadata.modpath}";
            if (path != $@"{System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Packages\\")
            {
                Utilities.ParallelLogger.Log($"[DEBUG] Message 6: path is set to {path}.");
                File.Create($"{path}/Test.txt").Dispose();
                Close();
            }
            else if (path == null)
            {
                Utilities.ParallelLogger.Log($"[ERROR] The path was not set.");
                Close();
            }
            else
            {
                Utilities.ParallelLogger.Log($"[DEBUG] Message 7: path is set to {path}.");
                Utilities.ParallelLogger.Log($"[ERROR] Failed to grab the mod metadata.");
                Close();
            }
        }
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            var openPng = new CommonOpenFileDialog();
            openPng.Filters.Add(new CommonFileDialogFilter("Preview", "*.*"));
            openPng.EnsurePathExists = true;
            openPng.EnsureValidNames = true;
            openPng.Title = "Select Preview";
            if (openPng.ShowDialog() == CommonFileDialogResult.Ok)
            {
                PreviewBox.Text = openPng.FileName;
                thumbnailPath = openPng.FileName;
            }
            // Bring Mod Config window back to foreground after closing dialog
            this.Activate();
        }
    }
}
