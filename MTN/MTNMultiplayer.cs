﻿using Galaxy.Api;
using Harmony;
using MTN.FarmInfo;
using Netcode;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.SDKs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MTN {

    /// <summary>
    /// Expanded Multiplayer class. Implements needed messages for MTN to work properly in Multiplayer.
    /// To be removed, refactor, and implemented into SMAPI at a later date.
    /// </summary>
    public class MTNMultiplayer : Multiplayer {
        private int tickCount = 0;
        private int minutesPassed = 0;

        private ulong downloadByteCount = 0;
        private ulong downloadByteAvg = 0;
        private ulong downloadByteTotal = 0;

        private ulong uploadByteCount = 0;
        private ulong uploadByteAvg = 0;
        private ulong uploadByteTotal = 0;

        public MTNMultiplayer() {
            Memory.multiplayer = this;
        }

        public void countTick() {
            tickCount++;
            if (tickCount >= 60) {
                minutesPassed++;
                downloadByteTotal += downloadByteCount;
                downloadByteAvg = (downloadByteTotal / (ulong)minutesPassed);

                if (Game1.client is MTNLidgrenClient) {
                    uploadByteCount = (Game1.client as MTNLidgrenClient).readUploadAmount();
                } else if (Game1.client is MTNGalaxyNetClient) {
                    uploadByteCount = (Game1.client as MTNGalaxyNetClient).readUploadAmount();
                }
                uploadByteTotal += uploadByteCount;
                uploadByteAvg = (uploadByteTotal / (ulong)minutesPassed);
                //Memory.instance.Monitor.Log("Minute update: " + byteCount + " bytes (" + byteAvg + " bytes/minute average)");
                Memory.instance.Monitor.Log("Minute update: " + downloadByteCount + "/" + uploadByteCount + " bytes [Download/Upload]");
                Memory.instance.Monitor.Log("Average: " + downloadByteAvg + "/" + uploadByteAvg + " bytes [Download/Upload] (" + minutesPassed + " minute(s))");
                tickCount = 0;
                downloadByteCount = 0;
                uploadByteCount = 0;
            }
        }

        public override Client InitClient(Client client) {
            if (client is GalaxyNetClient) {
                GalaxyID gid = (GalaxyID)Traverse.Create(client).Field("lobbyId").GetValue();
                MTNGalaxyNetClient wrappedClient = new MTNGalaxyNetClient(gid);
                return wrappedClient;
            } else if (client is LidgrenClient) {
                string address = (string)Traverse.Create(client).Field("address").GetValue();
                MTNLidgrenClient wrappedClient = new MTNLidgrenClient(address);
                return wrappedClient;
            } else {
                return client;
            }
        }

        public override void StartServer() {
            Game1.server = new MTNGameServer();
            Game1.server.startServer();
        }

        /// <summary>
        /// Reimplementation of activeLocations. Enables more than just the Farm, Farmhouse, and whatever map
        /// the player is currently on to remain active in memory.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<GameLocation> activeLocations() {
            if (Game1.currentLocation != null) {
                yield return Game1.currentLocation;
            }

            //Search for Farms & FarmHouse.
            foreach (GameLocation loc in Game1.locations) {
                if (loc != Game1.currentLocation) {
                    if (loc is Farm || loc is FarmHouse) {
                        yield return loc;
                    }
                    if (loc is BuildableGameLocation) {
                        foreach (Building b in (loc as BuildableGameLocation).buildings) {
                            if (b.indoors.Value != null && b.indoors.Value != Game1.currentLocation) {
                                yield return b.indoors.Value;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reimplemenation of isAlwaysActiveLocation to allow for more than 1 farm to exist in a single Multiplayer game.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public override bool isAlwaysActiveLocation(GameLocation location) {
            if (location is Farm || location is FarmHouse || (location.Root != null && location.Root.Value.Equals(Game1.getFarm()))) {
                return true;
            }
            return false;
        }

        public override void processIncomingMessage(IncomingMessage msg) {
            downloadByteCount += (ulong)msg.Data.Length;
            //Memory.instance.Monitor.Log("Message type: " + msg.MessageType);
            switch (msg.MessageType) {
                case 3:
                    readActiveLocation(msg, false);
                    break;
                case 6:
                    GameLocation gameLocation = readLocation(msg.Reader);
                    if (gameLocation == null)
                        break;
                    readObjectDelta<GameLocation>(msg.Reader, gameLocation.Root);
                    break;
                //MTN calls
                case 30:
                    setMTNCustom(msg);
                    break;
                case 31:
                    break;
                //MTN Tests for SMAPI
                case 50:
                    //Disabled due to bug.
                    //processModList(msg);
                    break;
                default:
                    base.processIncomingMessage(msg);
                    break;
            }
        }

        protected void setMTNCustom(IncomingMessage msg) {
            // convert byte to int
            Memory.loadCustomFarmType(BitConverter.ToInt32(msg.Data, 0));

            Utilities.additionalMapLoad();
        }

        public override NetRoot<GameLocation> locationRoot(GameLocation location) {
            if (location.Root == null && Game1.IsMasterGame) {
                new NetRoot<GameLocation>().Set(location);
                location.Root.Clock.InterpolationTicks = this.interpolationTicks();
                location.Root.MarkClean();
            }
            return location.Root;
        }

        protected override void readActiveLocation(IncomingMessage msg, bool forceCurrentLocation = false) {
            NetRoot<GameLocation> root = readObjectFull<GameLocation>(msg.Reader);
            if (isAlwaysActiveLocation(root.Value)) {
                Memory.instance.Monitor.Log("Recieving Map: " + root.Value.Name + " as Always Active.");

                var locations = Game1.locations.Where(x => x.Equals(root.Value));

                if (Game1.locations.Count(x => x.Equals(root.Value)) > 0)
                {
                    for (int i = 0; i < Game1.locations.Count; i++)
                    {
                        if (Game1.locations[i].Equals(root.Value))
                        {
                            Game1.locations[i] = root.Value;
                            if (Game1.locations[i] is BuildableGameLocation)
                            {
                                foreach (Building b in (Game1.locations[i] as BuildableGameLocation).buildings)
                                {
                                    b.load();
                                }
                            }
                            break;
                        }
                    }
                }
                else
                {
                    Game1.locations.Add(root.Value);
                    if (Game1.locations.Last() is BuildableGameLocation)
                    {
                        foreach (Building b in (Game1.locations.Last() as BuildableGameLocation).buildings)
                        {
                            b.load();
                        }
                    }
                }
            }
            if (Game1.locationRequest != null || forceCurrentLocation) {
                Memory.instance.Monitor.Log("Reciving Map: " + root.Value.Name + " as Current Location (disposible).");
                if (Game1.locationRequest != null) {
                    Game1.currentLocation = Game1.findStructure(root.Value, Game1.locationRequest.Name);
                    if (Game1.currentLocation == null) {
                        Game1.currentLocation = root.Value;
                    }
                } else if (forceCurrentLocation) {
                    Game1.currentLocation = root.Value;
                }
                if (Game1.locationRequest != null) {
                    Game1.locationRequest.Loaded(root.Value);
                }
                Game1.currentLocation.resetForPlayerEntry();
                Game1.player.currentLocation = Game1.currentLocation;
                if (Game1.locationRequest != null) {
                    Game1.locationRequest.Warped(root.Value);
                }
                Game1.currentLocation.updateSeasonalTileSheets();
                if (Game1.isDebrisWeather) {
                    Game1.populateDebrisWeatherArray();
                }
                Game1.locationRequest = null;
            }
        }
    }
}
