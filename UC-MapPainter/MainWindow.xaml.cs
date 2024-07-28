﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Collections.Generic;
using Path = System.IO.Path;

namespace UC_MapPainter
{
    public partial class MainWindow : Window
    {
        //UI
        public string currentEditMode;
        private ScaleTransform scaleTransform = new ScaleTransform();
        private GridModel gridModel = new GridModel();
        [Flags]
        public enum ButtonFlags
        {
            None = 0,
            Textures = 1 << 0,
            Height = 1 << 1,
            Buildings = 1 << 2,
            Prims = 1 << 3,
            All = Textures | Height | Buildings | Prims
        }

        //Map
        public byte[] loadedFileBytes;
        private string loadedFilePath;
        public byte[] modifiedFileBytes;
        public byte[] LoadedFileBytes
        {
            get { return loadedFileBytes; }
            set { loadedFileBytes = value; }
        }
        public string LoadedFilePath
        {
            get { return loadedFilePath; }
            set { loadedFilePath = value; }
        }
        public byte[] ModifiedFileBytes
        {
            get { return modifiedFileBytes; }
            set { modifiedFileBytes = value; }
        }
        public bool isNewFile = true;

        //Textures
        private TextureFunctions textureFunctions;
        private TextureSelectionWindow textureSelectionWindow;
        private int selectedWorldNumber;
        private bool isTextureSelectionLocked = false;
        private int lockedWorld = -1;
        private string selectedTextureType;
        private int selectedTextureNumber;
        private int selectedTextureRotation = 0;

        //Prims
        internal PrimSelectionWindow primSelectionWindow;
        private PrimFunctions primFunctions;
        private int selectedPrimNumber = -1;
        private Canvas MapWhoGridCanvas = new Canvas();
        private bool isMapWhoGridVisible = false;
        public int SelectedPrimNumber
        {
            get { return selectedPrimNumber; }
            set { selectedPrimNumber = value; }
        }

        //Windows
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            MainContentGrid.LayoutTransform = scaleTransform;
            MainContentGrid.MouseMove += MainContentGrid_MouseMove;
            MapWhoGridCanvas.IsHitTestVisible = false;
            MainContentGrid.Children.Add(MapWhoGridCanvas);
            primFunctions = new PrimFunctions(this, gridModel, primSelectionWindow);
            textureFunctions = new TextureFunctions(gridModel, selectedWorldNumber, this);
            this.Closing += MainWindow_Closing; // Add this line
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Check if there are unsaved changes
            CheckForUnsavedChanges();
        }

        private void InitializePrimSelectionWindow()
        {
            if (primSelectionWindow == null || !primSelectionWindow.IsLoaded)
            {
                primSelectionWindow = new PrimSelectionWindow();
                primSelectionWindow.SetMainWindow(this);
                primSelectionWindow.Left = this.Left + this.Width - primSelectionWindow.Width - 10;
                primSelectionWindow.Top = 50;
                primSelectionWindow.Closed += PrimSelectionWindow_Closed;
                primSelectionWindow.Show();
                primSelectionWindow.Owner = this; // Set the owner after showing the window
                PrimSelectionMenuItem.IsEnabled = false; // Disable the menu item
            }
        }
        private void InitializeTextureSelectionWindow()
        {
            if (textureSelectionWindow == null || !textureSelectionWindow.IsLoaded)
            {
                textureSelectionWindow = new TextureSelectionWindow();
                textureSelectionWindow.SetMainWindow(this);
                textureSelectionWindow.Left = this.Left + this.Width - textureSelectionWindow.Width - 10;
                textureSelectionWindow.Top = 50;
                textureSelectionWindow.Closed += TextureSelectionWindow_Closed;
                textureSelectionWindow.Show();
                textureSelectionWindow.Owner = this; // Set the owner after showing the window
                TextureSelectionMenuItem.IsEnabled = false; // Disable the menu item

                // Restore lock status and selected world
                if (isTextureSelectionLocked && lockedWorld != null)
                {
                    textureSelectionWindow.SetSelectedWorld(lockedWorld);
                    textureSelectionWindow.LockWorld();
                    textureSelectionWindow.LoadWorldTextures(lockedWorld);
                }
            }
        }


        ///////////////
        //Click Events
        ///////////////
        private void PrimSelection_Click(object sender, RoutedEventArgs e)
        {
            InitializePrimSelectionWindow();
        }
        private void TextureSelection_Click(object sender, RoutedEventArgs e)
        {
            InitializeTextureSelectionWindow();
        }

        private async void NewMap_Click(object sender, RoutedEventArgs e)
        {
            string newFile = "";
            isNewFile = true;
            LoadAsync(newFile);
        }

        private void LoadMap_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "IAM files (*.iam)|*.iam",
                Title = "Load Map"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                isNewFile = false;
                LoadAsync(filePath);
            }
        }

        private void SaveMap_Click(object sender, RoutedEventArgs e)
        {
            if (!isNewFile)
            {
                File.WriteAllBytes(loadedFilePath, modifiedFileBytes);
            }
            else 
            {
                SaveAsMap();
            }
        }
        private void SaveAsMap_Click(object sender, RoutedEventArgs e)
        {
            SaveAsMap();
        }

        public void SaveAsMap() 
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "IAM files (*.iam)|*.iam",
                Title = "Save Map As",
                FileName = "ExportedMap.iam"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string userFilePath = saveFileDialog.FileName;
                if (userFilePath.Length > 96)
                {
                    var result = MessageBox.Show(
                        "The file path exceeds the maximum length of 96 characters. Using the load map method of debug mode, the game cannot parse paths longer than 96 characters. Do you still want to save here?",
                        "File Path Too Long",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }
                }

                File.WriteAllBytes(userFilePath, modifiedFileBytes);
                loadedFilePath = userFilePath; // Store the loaded file path
                loadedFileBytes = File.ReadAllBytes(userFilePath);
                modifiedFileBytes = (byte[])loadedFileBytes.Clone(); // New File bytes will be manipulated via edit process
                UpdateWindowTitle(Path.GetFileName(userFilePath));
                isNewFile = false;
                MessageBox.Show($"Map saved to {userFilePath}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportMapToBmp_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "BMP files (*.bmp)|*.bmp",
                Title = "Export Map to BMP",
                FileName = "ExportedMap.bmp"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                ExportMapToBmp(filePath);
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            scaleTransform.ScaleX *= 1.1;
            scaleTransform.ScaleY *= 1.1;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            scaleTransform.ScaleX /= 1.1;
            scaleTransform.ScaleY /= 1.1;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void EditTextureButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode("Textures");
        }

        private void EditHeightButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode("Height");
        }

        private void EditBuildingsButton_Click(object sender, RoutedEventArgs e)
        {
            //SetEditMode("Height");
        }

        private void EditPrimsButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode("Prims");
            InitializePrimSelectionWindow();
            primFunctions.DrawPrims(OverlayGrid);
        }

        private void ToggleMapWhoGridMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (isMapWhoGridVisible)
            {
                MapWhoGridCanvas.Children.Clear();
                ToggleMapWhoGridMenuItem.Header = "Draw MapWho Grid";
            }
            else
            {
                DrawMapWhoGrid();
                ToggleMapWhoGridMenuItem.Header = "Hide MapWho Grid";
            }
            isMapWhoGridVisible = !isMapWhoGridVisible;
        }

        private void ShowPrimInfo_Click(object sender, RoutedEventArgs e)
        {
            primFunctions.DisplayObjectData();
        }

        private void ShowMapWhoInfo_Click(object sender, RoutedEventArgs e)
        {
            primFunctions.DisplayMapWhoInfo();
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            selectedTextureRotation = (selectedTextureRotation - 90) % 360;
            if (selectedTextureRotation < 0)
            {
                selectedTextureRotation += 360;
            }
            ApplyRotation();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            selectedTextureRotation = (selectedTextureRotation + 90) % 360;
            ApplyRotation();
        }

        ///////////////
        //Events
        ///////////////
        private void PrimSelectionWindow_Closed(object sender, EventArgs e)
        {
            PrimSelectionMenuItem.IsEnabled = true; // Enable the menu item when the window is closed
        }

        private void TextureSelectionWindow_Closed(object sender, EventArgs e)
        {
            TextureSelectionMenuItem.IsEnabled = true; // Enable the menu item when the window is closed

            // Track lock status and selected world
            isTextureSelectionLocked = textureSelectionWindow.IsWorldLocked;
            lockedWorld = textureSelectionWindow.GetSelectedWorld();
        }

        private void MainContentGrid_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(MainContentGrid);

            int row = 127 - (int)(position.Y / 64);
            int col = 127 - (int)(position.X / 64);

            if (row >= 0 && row < 128 && col >= 0 && col < 128)
            {
                MousePositionLabel.Content = $"X: {col}, Z: {row}";
            }

            double pixelX = 8192 - position.X;
            double pixelZ = 8192 - position.Y;
            PixelPositionLabel.Content = $"Pixel: ({pixelX:F0}, {pixelZ:F0})";
        }

        //////////////////
        /// Miscellaneous
        /////////////////

        private async void LoadAsync(string filePath)
        {
            // Check if there are unsaved changes
            if (CheckForUnsavedChanges())
            {
                // Changes were saved, proceed with loading the new map
            }

            // Clear the arrays
            gridModel.PrimArray.Clear();
            gridModel.MapWhoArray.Clear();
            
            //Clear selected texture so previous world textures may not persist
            ClearSelectedTexture();

            //Close Windows if open
            // Close the PrimSelectionWindow if it is open
            if (primSelectionWindow != null && primSelectionWindow.IsLoaded)
            {
                primSelectionWindow.Close();
                primSelectionWindow = null;
            }

            // Close the TextureSelectionWindow if it is open
            if (textureSelectionWindow != null && textureSelectionWindow.IsLoaded)
            {
                textureSelectionWindow.Close();
                textureSelectionWindow = null;
            }

            // Clear the MainContentGrid if populated from previous load
            MainContentGrid.Children.Clear();
            MainContentGrid.RowDefinitions.Clear();
            MainContentGrid.ColumnDefinitions.Clear();
            gridModel.Cells.Clear();

            if (isNewFile)
            {
                // Load the default.iam file from embedded resources
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "UC_MapPainter.Map.default.iam"; // Adjust the namespace if necessary

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    loadedFileBytes = memoryStream.ToArray();
                }
                filePath = "New Unsaved Map"; // Store the loaded file path
            }
            else
            {
                // Load the file from the specified file path
                loadedFilePath = filePath; // Store the loaded file path
                loadedFileBytes = File.ReadAllBytes(filePath);
            }
            modifiedFileBytes = (byte[])loadedFileBytes.Clone(); // New File bytes will be manipulated via edit process
            UpdateWindowTitle(Path.GetFileName(filePath));

            var loadingWindow = new LoadingWindow()
            {
                TaskDescription = "Loading Map"
            };
            loadingWindow.Owner = this;
            loadingWindow.Show();

            // Determine the world number
            loadingWindow.TaskDescription = "Getting World Number";
            selectedWorldNumber = Map.ReadTextureWorld(loadedFileBytes, Map.ReadMapSaveType(loadedFileBytes));

            // Validate the world number
            if (!textureFunctions.IsValidWorld(selectedWorldNumber))
            {
                MessageBox.Show("World not assigned to Map. Please select a world.", "Invalid World", MessageBoxButton.OK, MessageBoxImage.Warning);
                var worldSelectionWindow = new WorldSelectionWindow();
                if (worldSelectionWindow.ShowDialog() == true)
                {
                    selectedWorldNumber = int.Parse(worldSelectionWindow.SelectedWorld);
                }
                else
                {
                    return;
                }
            }

            loadingWindow.Close();

            MessageBox.Show("File loaded successfully. You can now edit textures, heights, or prims.", "Load Successful", MessageBoxButton.OK, MessageBoxImage.Information);

            SaveMenuItem.IsEnabled = true;
            SaveAsMenuItem.IsEnabled = true;
            ExportMenuItem.IsEnabled = true; // Enable the Export menu item

            ModifyButtonStatus(ButtonFlags.All);
        }

        public void Cell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border cell)
            {
                var position = e.GetPosition(MainContentGrid);
                int pixelX = (int)position.X;
                int pixelZ = (int)position.Y;

                if (currentEditMode == "Textures")
                {
                    int row = 127 - Grid.GetRow(cell);
                    int col = 127 - Grid.GetColumn(cell);
                    int cellOffset = textureFunctions.FindCellTexOffset(col, row);

                    var cellData = gridModel.Cells.FirstOrDefault(c => c.Row == row && c.Column == col);
                    if (cellData != null)
                    {
                        if (SelectedTextureImage.Source != null)
                        {
                            var imageBrush = new ImageBrush
                            {
                                ImageSource = SelectedTextureImage.Source,
                                Stretch = Stretch.None,
                                AlignmentX = AlignmentX.Center,
                                AlignmentY = AlignmentY.Center
                            };

                            var rotateTransform = new RotateTransform(selectedTextureRotation, 32, 32);

                            cell.Background = new VisualBrush
                            {
                                Visual = new Image
                                {
                                    Source = SelectedTextureImage.Source,
                                    RenderTransform = rotateTransform,
                                    RenderTransformOrigin = new Point(0.5, 0.5),
                                    Stretch = Stretch.Fill,
                                    Width = 64,
                                    Height = 64
                                }
                            };

                            bool isDefaultTexture = selectedTextureNumber == 0 && selectedTextureType == "world";
                            cellData.TextureType = selectedTextureType;
                            cellData.TextureNumber = selectedTextureNumber;
                            cellData.Rotation = selectedTextureRotation;
                            cellData.UpdateTileSequence(isDefaultTexture, cellOffset);
                        }
                    }
                }

                else if (currentEditMode == "Height")
                {
                    int row = 127 - Grid.GetRow(cell);
                    int col = 127 - Grid.GetColumn(cell);
                    int cellOffset = textureFunctions.FindCellTexOffset(col, row);

                    var cellData = gridModel.Cells.FirstOrDefault(c => c.Row == row && c.Column == col);
                    if (cellData != null)
                    {
                        int increment = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10 : 1;
                        cellData.Height = Math.Min(cellData.Height + increment, 127);
                        cellData.UpdateTileHeight(cellOffset);
                        if (cell.Child is TextBlock textBlock)
                        {
                            textBlock.Text = cellData.Height.ToString();
                        }
                        else
                        {
                            cell.Child = new TextBlock
                            {
                                Text = cellData.Height.ToString(),
                                Foreground = Brushes.Red,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0, 0, 5, 5),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom
                            };
                        }
                    }
                }

                else if (currentEditMode == "Buildings")
                {
                    //do something
                }

                else if (currentEditMode == "Prims" && selectedPrimNumber != -1)
                {
                    // Calculate necessary values
                    int mapWhoRow = 31 - (pixelZ / 256);
                    int mapWhoCol = 31 - (pixelX / 256);
                    int mapWhoIndex = mapWhoCol * 32 + mapWhoRow;
                    int relativeX = pixelX % 256;
                    int relativeZ = pixelZ % 256;
                    int globalTileX = pixelX / 64;
                    int globalTileZ = pixelZ / 64;

                    // Get the MapWho cell
                    var mapWho = gridModel.MapWhoArray[mapWhoIndex];

                    // Check if the total number of objects exceeds 2000
                    if (gridModel.TotalPrimCount >= 2000)
                    {
                        MessageBox.Show("Can't place Object. Maximum number of objects in the map is 2000", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return; // Exit the method without placing the prim
                    }

                    // Check if the MapWho cell already contains 31 prims
                    if (gridModel.MapWhoPrimCounts[mapWhoIndex] >= 31)
                    {
                        MessageBox.Show("Can't place Object. Maximum number of objects per MapWho cell is 31", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return; // Exit the method without placing the prim
                    }

                    // Get the current yaw from the PrimSelectionWindow
                    byte currentYaw = primSelectionWindow.GetCurrentYaw();
                    byte flags = primSelectionWindow.GetFlagsValue();
                    short currentHeight = primSelectionWindow.GetCurrentHeight();

                    // Create the new Prim object
                    var newPrim = new Prim
                    {
                        PrimNumber = (byte)selectedPrimNumber,
                        X = (byte) (256 - relativeX),
                        Z = (byte) (256 - relativeZ),
                        Y = currentHeight, // Initial Y position, you can modify this later
                        Yaw = currentYaw, // Initial yaw from the PrimSelectionWindow
                        Flags = flags, // Set the flags
                        InsideIndex = 0 // Initial inside idx, modify as necessary
                    };

                    // Add the new Prim to the grid model
                    gridModel.PrimArray.Add(newPrim);

                    // Update the MapWhoPrimCounts and TotalPrimCount
                    gridModel.MapWhoPrimCounts[mapWhoIndex]++;
                    gridModel.TotalPrimCount++;

                    primFunctions.PlacePrim(newPrim, pixelX, pixelZ, mapWhoIndex, mapWhoRow, mapWhoCol, relativeX, relativeZ, globalTileX, globalTileZ, OverlayGrid);

                    // Reset the selected prim
                    selectedPrimNumber = -1;
                    if (primSelectionWindow != null && primSelectionWindow.IsLoaded)
                    {
                        primSelectionWindow.UpdateSelectedPrimImage(-1); // Clear the selected prim image
                    }

                    //primFunctions.UpdatePrimAndMapWhoSections(modifiedFileBytes, out modifiedFileBytes);
                    //Update MapWho and Object Section
                    primFunctions.RebuildMapWhoAndPrimArrays(out List<Prim> newPrimArray, out List<MapWho> newMapWhoArray);

                    // Calculate the original object offset and objectSectionSize
                    int saveType = Map.ReadMapSaveType(modifiedFileBytes);
                    int objectBytes = Map.ReadObjectSize(modifiedFileBytes);

                    //Get the physical size of the object section
                    int size = Map.ReadObjectSectionSize(modifiedFileBytes, saveType, objectBytes);

                    // Calculate the object offset
                    int objectOffset = size + 8;
                    // Retrieve the original number of objects
                    int originalNumObjects = Map.ReadNumberPrimObjects(modifiedFileBytes, objectOffset);

                    // Determine the new object section size and number of objects
                    int objectSectionSize = ((newPrimArray.Count + 1) * 8) + 4 + 2048;
                    int numObjects = newPrimArray.Count + 1;

                    // Calculate the difference in object count
                    int objectDifference = numObjects - originalNumObjects;

                    //Old MapWho Offset
                    int originalMapWhoOffset = objectOffset + 4 + (originalNumObjects * 8);

                    // Calculate the new file size
                    int newFileSize = modifiedFileBytes.Length + (objectDifference * 8);

                    // Create the new transitory fileBytes containing the updated object data.
                    byte[] swapFileBytes = new byte[newFileSize];

                    // Copy existing data up to the object offset
                    Array.Copy(modifiedFileBytes, swapFileBytes, objectOffset);

                    // Write the updated object section size in the transitory file
                    Map.WriteObjectSize(swapFileBytes, objectSectionSize);

                    //Write new prims
                    Map.WritePrims(swapFileBytes,newPrimArray,objectOffset);

                    // Insert the new MapWho data after the object data
                    int mapWhoOffset = objectOffset + 4 + ((newPrimArray.Count + 1) * 8);

                    Map.WriteMapWho(swapFileBytes, newMapWhoArray, mapWhoOffset);

                    // Copy any remaining data from the original file
                    if (modifiedFileBytes.Length > originalMapWhoOffset + 2048)
                    {
                        Array.Copy(modifiedFileBytes, originalMapWhoOffset + 2048, swapFileBytes, mapWhoOffset + 2048, modifiedFileBytes.Length - (originalMapWhoOffset + 2048));
                    }

                   modifiedFileBytes = swapFileBytes;
                }
            }
        }

        public void Cell_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed && sender is Border cell)
            {
                if (currentEditMode == "Textures")
                {
                    //Do something
                }
                else if (currentEditMode == "Height")
                {
                    int row = 127 - Grid.GetRow(cell);
                    int col = 127 - Grid.GetColumn(cell);
                    int cellOffset = textureFunctions.FindCellTexOffset(col, row);

                    var cellData = gridModel.Cells.FirstOrDefault(c => c.Row == row && c.Column == col);
                    if (cellData != null)
                    {
                        int decrement = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10 : 1;
                        cellData.Height = Math.Max(cellData.Height - decrement, -127);
                        cellData.UpdateTileHeight(cellOffset);
                        if (cell.Child is TextBlock textBlock)
                        {
                            textBlock.Text = cellData.Height.ToString();
                        }
                        else
                        {
                            cell.Child = new TextBlock
                            {
                                Text = cellData.Height.ToString(),
                                Foreground = Brushes.Red,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0, 0, 5, 5),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom
                            };
                        }
                    }
                }
                else if (currentEditMode == "Buildings")
                {
                    //Do something
                }
                else if (currentEditMode == "Prims")
                {
                   //do something
                }

                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    int row = 127 - Grid.GetRow(cell);
                    int col = 127 - Grid.GetColumn(cell);

                    var cellData = gridModel.Cells.FirstOrDefault(c => c.Row == row && c.Column == col);
                    if (cellData != null)
                    {
                        string textureFolder = cellData.TextureType == "world" ? $"world{selectedWorldNumber}" : cellData.TextureType;
                        string texturePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures", textureFolder, $"tex{cellData.TextureNumber:D3}hi.bmp");

                        string debugMessage = $"Cell Debug Information:\n" +
                                              $"X: {col}\n" +
                                              $"Y: {row}\n" +
                                              $"Texture File Path: {texturePath}\n" +
                                              $"Texture Type: {cellData.TextureType}\n" +
                                              $"Rotation: {cellData.Rotation}°\n" +
                                              $"Height: {cellData.Height}\n" +
                                              $"Tile Bytes: {BitConverter.ToString(cellData.TileSequence)}"; // Use the stored tile bytes
                        MessageBox.Show(debugMessage, "Cell Debug Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void ModifyButtonStatus(ButtonFlags flags)
        {
            EditTextureButton.IsEnabled = (flags & ButtonFlags.Textures) != 0;
            EditHeightButton.IsEnabled = (flags & ButtonFlags.Height) != 0;
            EditBuildingsButton.IsEnabled = (flags & ButtonFlags.Buildings) != 0;
            EditPrimsButton.IsEnabled = (flags & ButtonFlags.Prims) != 0;
        }
        private void ExportMapToBmp(string filePath)
        {
            const int cellSize = 64;
            const int gridSize = 128;
            const int imageSize = cellSize * gridSize;

            var renderTargetBitmap = new RenderTargetBitmap(imageSize, imageSize, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                foreach (var cell in gridModel.Cells)
                {
                    var texturePath = textureFunctions.GetTexturePath(cell.TextureType, cell.TextureNumber);
                    if (File.Exists(texturePath))
                    {
                        var imageSource = new BitmapImage(new Uri(texturePath));
                        var rect = new Rect(cell.Column * cellSize, cell.Row * cellSize, cellSize, cellSize);

                        drawingContext.PushTransform(new RotateTransform(cell.Rotation + 180, rect.X + cellSize / 2, rect.Y + cellSize / 2));
                        drawingContext.DrawImage(imageSource, rect);
                        drawingContext.Pop();
                    }
                }
            }

            renderTargetBitmap.Render(drawingVisual);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                var bitmapEncoder = new BmpBitmapEncoder();
                bitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
                bitmapEncoder.Save(fileStream);
            }

            MessageBox.Show($"Map exported to {filePath}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void UpdateSelectedTexture(ImageSource newTexture, string type, int number)
        {
            SelectedTextureImage.Source = newTexture;
            selectedTextureType = type;
            selectedTextureNumber = number;
            selectedTextureRotation = 0; // Reset rotation when a new texture is selected

            // Apply rotation
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            var transform = new RotateTransform(selectedTextureRotation)
            {
                CenterX = SelectedTextureImage.Width / 2,
                CenterY = SelectedTextureImage.Height / 2
            };
            SelectedTextureImage.RenderTransform = transform;
        }

        private async void SetEditMode(string mode)
        {
            currentEditMode = mode;

            var loadingWindow = new LoadingWindow();
            loadingWindow.Owner = this;

            switch (mode)
            {
                case "Textures":
                    InitializeTextureSelectionWindow();
                    textureSelectionWindow.SetSelectedWorld(selectedWorldNumber);
                    textureSelectionWindow.LockWorld();
                    textureSelectionWindow.LoadWorldTextures(selectedWorldNumber);
                    ModifyButtonStatus(ButtonFlags.Height | ButtonFlags.Buildings | ButtonFlags.Prims);
                    OverlayGrid.Visibility = Visibility.Collapsed;
                    ClearSelectedTexture();
                    loadingWindow.TaskDescription = "Loading Textures";
                    loadingWindow.Show();
                    await textureFunctions.DrawCells(modifiedFileBytes, selectedWorldNumber);
                    loadingWindow.Close();
                    break;
                case "Height":
                    ModifyButtonStatus(ButtonFlags.Textures | ButtonFlags.Buildings | ButtonFlags.Prims);
                    OverlayGrid.Visibility = Visibility.Collapsed;
                    loadingWindow.TaskDescription = "Loading Heights";
                    loadingWindow.Show();
                    await textureFunctions.DrawCells(modifiedFileBytes, selectedWorldNumber);
                    ClearSelectedTexture();
                    loadingWindow.Close();
                    break;
                case "Buildings":
                    ModifyButtonStatus(ButtonFlags.Textures | ButtonFlags.Height | ButtonFlags.Prims);
                    OverlayGrid.Visibility = Visibility.Collapsed;
                    ClearSelectedTexture();
                    break;
                case "Prims":
                    ModifyButtonStatus(ButtonFlags.Textures | ButtonFlags.Height | ButtonFlags.Buildings);
                    ClearSelectedTexture();
                    InitializePrimSelectionWindow(); // Open the PrimNumber Selection Window
                    // Get Save Type
                    loadingWindow.TaskDescription = "Getting Map Save Type";
                    int saveType = Map.ReadMapSaveType(modifiedFileBytes);
                    loadingWindow.TaskDescription = "Getting Size of the Object Section";
                    loadingWindow.Show();
                    int objectSectionSize = Map.ReadObjectSize(modifiedFileBytes);
                    loadingWindow.TaskDescription = "Reading Prim Data";
                    if (MainContentGrid.Children.Count == 0)
                    {
                        OverlayGrid.Visibility = Visibility.Collapsed;
                        await textureFunctions.DrawCells(modifiedFileBytes, selectedWorldNumber);
                    }
                    OverlayGrid.Visibility = Visibility.Visible;
                    await primFunctions.ReadObjectData(modifiedFileBytes, saveType, objectSectionSize);
                    loadingWindow.Close();
                    break;
                default:
                    ModifyButtonStatus(ButtonFlags.All); // Enable all buttons by default
                    break;
            }
        }

        private void DrawMapWhoGrid()
        {
            primFunctions.DrawMapWhoGrid(MapWhoGridCanvas);
            EnsureMapWhoGridCanvasOnTop();
        }

        private void EnsureMapWhoGridCanvasOnTop()
        {
            MainContentGrid.Children.Remove(MapWhoGridCanvas);
            MainContentGrid.Children.Add(MapWhoGridCanvas);
        }

        private void ClearSelectedTexture()
        {
            SelectedTextureImage.Source = null;
            selectedTextureType = string.Empty;
            selectedTextureNumber = -1;
            selectedTextureRotation = 0;
        }

        private void UpdateWindowTitle(string fileName)
        {
            this.Title = $"Urban Chaos Map Editor - {fileName}";
        }

        public void UpdateSelectedPrim(int primNumber)
        {
            primFunctions.UpdateSelectedPrim(primNumber);
        }

        private bool CheckForUnsavedChanges()
        {
            if (loadedFileBytes != null && modifiedFileBytes != null && !loadedFileBytes.SequenceEqual(modifiedFileBytes))
            {
                var result = MessageBox.Show("It looks like there have been changes to the currently loaded map, would you like to save them?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    SaveMap_Click(null, null);
                    return true; // Changes were saved
                }
            }
            return false; // No changes or user chose not to save
        }


    }
}
