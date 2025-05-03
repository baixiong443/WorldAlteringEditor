﻿using Rampastring.Tools;
using System;
using System.Collections.Generic;
using Rampastring.XNAUI.Input;
using TSMapEditor.GameMath;
using TSMapEditor.Models;
using TSMapEditor.Mutations.Classes;
using Rampastring.XNAUI;
using Microsoft.Xna.Framework;
using System.Linq;
using TSMapEditor.UI.Controls;
using System.IO;

namespace TSMapEditor.UI.CursorActions
{
    /// <summary>
    /// A cursor action that allows pasting previously copied terrain.
    /// </summary>
    public class PasteTerrainCursorAction : CursorAction
    {
        public PasteTerrainCursorAction(ICursorActionTarget cursorActionTarget, RKeyboard keyboard) : base(cursorActionTarget)
        {
            this.keyboard = keyboard;
        }

        public override string GetName() => "Paste Copied Terrain";

        public override bool HandlesKeyboardInput => true;

        struct OriginalOverlayInfo
        {
            public Point2D CellCoords;
            public OverlayType OverlayType;
            public int FrameIndex;

            public OriginalOverlayInfo(Point2D cellCoords, OverlayType overlayType, int frameIndex)
            {
                CellCoords = cellCoords;
                OverlayType = overlayType;
                FrameIndex = frameIndex;
            }
        }

        struct OriginalSmudgeInfo
        {
            public Point2D CellCoords;
            public SmudgeType SmudgeType;


        }


        private CopiedMapData copiedMapData;

        private List<OriginalOverlayInfo> originalOverlay = new List<OriginalOverlayInfo>();

        private RKeyboard keyboard;

        private int originLevelOffset;

        private bool wasDrawnAbove;


        private Point2D[][] edges { get; set; } = new Point2D[][] { Array.Empty<Point2D>() };

        public override void OnKeyPressed(KeyPressEventArgs e, Point2D cellCoords)
        {
            if (Constants.IsFlatWorld)
                return;

            if (KeyboardCommands.Instance.AdjustTileHeightDown.Key.Key == e.PressedKey)
            {
                if (originLevelOffset > -Constants.MaxMapHeight)
                    originLevelOffset--;

                e.Handled = true;
            }
            else if (KeyboardCommands.Instance.AdjustTileHeightUp.Key.Key == e.PressedKey)
            {
                if (originLevelOffset < Constants.MaxMapHeight)
                    originLevelOffset++;

                e.Handled = true;
            }
        }

        public override void OnActionEnter()
        {
            base.OnActionEnter();

            originLevelOffset = 0;

            if (!System.Windows.Forms.Clipboard.ContainsData(Constants.ClipboardMapDataFormatValue))
            {
                Logger.Log(nameof(PasteTerrainCursorAction) + ": invalid clipboard data format, exiting action");
                ExitAction();
                return;
            }

            byte[] data;
            object dataObject = (byte[])System.Windows.Forms.Clipboard.GetData(Constants.ClipboardMapDataFormatValue);
            if (dataObject is byte[] byteArray)
            {
                data = byteArray;
            }
            else if (dataObject is MemoryStream memoryStream)
            {
                // Some users have reported that clipboard data is retrieved as MemoryStream for them,
                // despite that the editor always saves them in byte[] format.
                Logger.Log("Warning: Handling clipboard data as MemoryStream");
                data = new byte[memoryStream.Length];
                memoryStream.Read(data, 0, (int)memoryStream.Length);
                memoryStream.Dispose();
            }
            else
            {
                Logger.Log($"{nameof(PasteTerrainCursorAction)}: unknown object type {dataObject.GetType().Name} stored to clipboard, exiting action");
                ExitAction();
                return;
            }

            try
            {
                copiedMapData = new CopiedMapData();
                copiedMapData.Deserialize(data);
                GenerateGraphicalEdges();
            }
            catch (CopiedMapDataSerializationException ex)
            {
                Logger.Log(nameof(PasteTerrainCursorAction) + ": exception when decoding data from clipboard, exiting action. Message: " + ex.Message);
                ExitAction();
            }
        }

        private void GenerateGraphicalEdges()
        {
            var foundationHashSet = new HashSet<Point2D>();

            copiedMapData.CopiedMapEntries.ForEach(entry =>
            {
                foundationHashSet.Add(entry.Offset);
            });

            edges = Helpers.CreateEdges(copiedMapData.Width + 2, copiedMapData.Height + 2, foundationHashSet.ToList());
        }

        public override void PreMapDraw(Point2D cellCoords)
        {
            originalOverlay.Clear();

            if (KeyboardCommands.Instance.PlaceTerrainBelow.AreKeysOrModifiersDown(Keyboard))
            {
                wasDrawnAbove = true;
                cellCoords -= new Point2D(copiedMapData.Width, copiedMapData.Height);
            }
            else
            {
                wasDrawnAbove = false;
            }

            int maxOffset = 0;
            MapTile originCell = MutationTarget.Map.GetTile(cellCoords);
            int originLevel = originCell?.Level ?? -1;

            foreach (var entry in copiedMapData.CopiedMapEntries)
            {
                maxOffset = Math.Max(maxOffset, Math.Max(Math.Abs(entry.Offset.X), Math.Abs(entry.Offset.Y)));

                MapTile cell = CursorActionTarget.Map.GetTile(cellCoords + entry.Offset);
                if (cell == null)
                    continue;

                if (entry.EntryType == CopiedEntryType.Terrain)
                {
                    var terrainEntry = entry as CopiedTerrainEntry;
                    cell.PreviewTileImage = CursorActionTarget.TheaterGraphics.GetTileGraphics(terrainEntry.TileIndex, 0);
                    cell.PreviewSubTileIndex = terrainEntry.SubTileIndex;
                    cell.PreviewLevel = Math.Max(0, Math.Min(Constants.MaxMapHeightLevel, originLevel + terrainEntry.HeightOffset + originLevelOffset));
                }
                else if (entry.EntryType == CopiedEntryType.Overlay)
                {
                    var overlayEntry = entry as CopiedOverlayEntry;

                    // Store original overlay info
                    if (cell.Overlay != null)
                        originalOverlay.Add(new OriginalOverlayInfo(cell.CoordsToPoint(), cell.Overlay.OverlayType, cell.Overlay.FrameIndex));
                    else
                        originalOverlay.Add(new OriginalOverlayInfo(cell.CoordsToPoint(), null, Constants.NO_OVERLAY));

                    var overlayType = Map.Rules.OverlayTypes.Find(ot => ot.ININame == overlayEntry.OverlayTypeName);
                    if (overlayType == null) 
                    {
                        continue;
                    }

                    // Apply new overlay info
                    if (cell.Overlay == null)
                    {
                        // Creating new object instances each frame is not very performance-friendly, we might want to revise this later...
                        cell.Overlay = new Overlay()
                        {
                            Position = cell.CoordsToPoint(),
                            OverlayType = overlayType,
                            FrameIndex = overlayEntry.FrameIndex
                        };
                    }
                    else
                    {
                        cell.Overlay.OverlayType = overlayType;
                        cell.Overlay.FrameIndex = overlayEntry.FrameIndex;
                    }
                }
            }

            CursorActionTarget.AddRefreshPoint(cellCoords, maxOffset);
        }

        public override void PostMapDraw(Point2D cellCoords)
        {
            if (wasDrawnAbove)
            {
                cellCoords -= new Point2D(copiedMapData.Width, copiedMapData.Height);
            }

            int maxOffset = 0;

            foreach (var copiedTerrain in copiedMapData.CopiedMapEntries)
            {
                if (copiedTerrain.EntryType != CopiedEntryType.Terrain)
                    continue;

                maxOffset = Math.Max(maxOffset, Math.Max(Math.Abs(copiedTerrain.Offset.X), Math.Abs(copiedTerrain.Offset.Y)));

                MapTile cell = CursorActionTarget.Map.GetTile(cellCoords + copiedTerrain.Offset);
                if (cell == null)
                    continue;

                cell.PreviewTileImage = null;
            }

            foreach (var originalOverlayEntry in originalOverlay)
            {
                MapTile cell = Map.GetTile(originalOverlayEntry.CellCoords);

                if (originalOverlayEntry.OverlayType == null)
                {
                    cell.Overlay = null;
                }
                else
                {
                    cell.Overlay.OverlayType = originalOverlayEntry.OverlayType;
                    cell.Overlay.FrameIndex = originalOverlayEntry.FrameIndex;
                }
            }

            CursorActionTarget.AddRefreshPoint(cellCoords, maxOffset);
        }

        public override void DrawPreview(Point2D cellCoords, Point2D cameraTopLeftPoint)
        {
            if (KeyboardCommands.Instance.PlaceTerrainBelow.AreKeysOrModifiersDown(Keyboard))
            {
                cellCoords -= new Point2D(copiedMapData.Width, copiedMapData.Height);
            }

            foreach (var edge in edges)
            {
                Point2D edgeCell0 = cellCoords + edge[0];
                Point2D edgeCell1 = cellCoords + edge[1];
                int heightOffset0 = 0;
                int heightOffset1 = 0;

                if (!CursorActionTarget.Is2DMode)
                {
                    var cell = Map.GetTile(edgeCell0);
                    if (cell != null)
                        heightOffset0 = Constants.CellHeight * cell.Level;

                    cell = Map.GetTile(edgeCell1);
                    if (cell != null)
                        heightOffset1 = Constants.CellHeight * cell.Level;
                }

                // Translate edge vertices from cell coordinate space to world coordinate space.
                var start = CellMath.CellTopLeftPointFromCellCoords(edgeCell0, Map) - cameraTopLeftPoint;
                var end = CellMath.CellTopLeftPointFromCellCoords(edgeCell1, Map) - cameraTopLeftPoint;
                // Height is an illusion, just move everything up or down.
                // Also offset X to match the top corner of an iso tile.
                start += new Point2D(Constants.CellSizeX / 2, -heightOffset0);
                end += new Point2D(Constants.CellSizeX / 2, -heightOffset1);

                start = start.ScaleBy(CursorActionTarget.Camera.ZoomLevel);
                end = end.ScaleBy(CursorActionTarget.Camera.ZoomLevel);

                // Draw edge.
                Renderer.DrawLine(start.ToXNAVector(), end.ToXNAVector(), Color.Orange, 2);
            }
        }

        public override void LeftClick(Point2D cellCoords)
        {
            if (CursorActionTarget.Map.GetTile(cellCoords) == null)
                return;

            bool allowOverlap = KeyboardCommands.Instance.OverlapObjects.AreKeysOrModifiersDown(keyboard);

            if (KeyboardCommands.Instance.PlaceTerrainBelow.AreKeysOrModifiersDown(Keyboard))
            {
                cellCoords -= new Point2D(copiedMapData.Width, copiedMapData.Height);
            }

            var mutation = new PasteTerrainMutation(CursorActionTarget.MutationTarget, copiedMapData, cellCoords, allowOverlap, originLevelOffset);
            CursorActionTarget.MutationManager.PerformMutation(mutation);
        }
    }
}
