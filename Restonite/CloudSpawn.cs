﻿using Elements.Core;
using FrooxEngine;
using FrooxEngine.Undo;
using System;
using System.Linq;

namespace Restonite
{
    public static class CloudSpawn
    {
        #region Public Methods

        public static Slot GetStatueSystem(Slot scratchSpace, CloudValueVariable<string> uriVariable, SyncRef<Slot> fallback)
        {
            if (fallback.Value != RefID.Null)
            {
                Log.Info($"Using statue system override from {fallback.Target.ToShortString()}");

                return fallback.Target.Duplicate(scratchSpace);
            }
            else
            {
                Log.Info("Getting statue system from cloud");
                // Yoinked from FrooxEngine.FileMetadata.OnImportFile
                var fileName = uriVariable.Value.Value;
                var fileUri = new Uri(fileName);

                var record = scratchSpace.Engine.RecordManager.FetchRecord(fileUri).GetAwaiter().GetResult();

                Log.Debug($"Got record for {record.Entity.Name}");
                Log.Debug($"Fetching from {record.Entity.AssetURI}");

                string fileData = scratchSpace.Engine.AssetManager.GatherAssetFile(new Uri(record.Entity.AssetURI), 100.0f).GetAwaiter().GetResult();

                if (fileData != null)
                {
                    scratchSpace.LocalUser.GetPointInFrontOfUser(out var point, out var rotation, float3.Backward);

                    Log.Info("Got file successfully");

                    return SpawnSlot(scratchSpace, fileData, scratchSpace.World, point, new float3(1.0f, 1.0f, 1.0f));
                }
                else
                {
                    Log.Error("ERROR: File was null after RequestGather");

                    return scratchSpace.AddSlot("File was null after RequestGather");
                }
            }
        }

        #endregion

        #region Private Methods

        private static Slot SpawnSlot(Slot x, string file, World world, float3 position, float3 scale)
        {
            DataTreeDictionary loadNode = DataTreeConverter.Load(file);

            Slot slot = x.AddSlot("SpawnSlotObject");
            slot.CreateSpawnUndoPoint();
            slot.LoadObject(loadNode);
            slot.GlobalPosition = position;
            slot.GlobalScale = scale;

            return slot.Children.First();
        }

        #endregion
    }
}
