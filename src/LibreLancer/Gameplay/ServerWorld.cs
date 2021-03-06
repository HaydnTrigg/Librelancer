// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace LibreLancer
{
    public class ServerWorld
    {
        private Dictionary<Player, GameObject> Players = new Dictionary<Player, GameObject>();
        ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();
        public GameWorld GameWorld;
        public GameServer Server;
        public GameData.StarSystem System;

        private int mId = -1;
        object _idLock = new object();
        int GenerateID()
        {
            lock (_idLock)
            {
                var retVal = mId--;
                if (mId < int.MinValue + 2) mId = -1;
                return retVal;
            }
        }
        
        public ServerWorld(GameData.StarSystem system, GameServer server)
        {
            Server = server;
            System = system;
            GameWorld = new GameWorld(null);
            GameWorld.Server = this;
            GameWorld.PhysicsUpdate += GameWorld_PhysicsUpdate;
            GameWorld.LoadSystem(system, server.Resources);
        }

        public int PlayerCount;

        public void SpawnPlayer(Player player, Vector3 position, Quaternion orientation)
        {
            Interlocked.Increment(ref PlayerCount);
            actions.Enqueue(() =>
            {
                foreach (var p in Players)
                {
                    player.SpawnPlayer(p.Key);
                    p.Key.SpawnPlayer(player);
                }
                player.SendSolars(SpawnedSolars);
                var obj = new GameObject() { World = GameWorld };
                obj.NetID = player.ID;
                GameWorld.Objects.Add(obj);
                Players[player] = obj;
                Players[player].Transform = Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(position);
            });
        }

        public void RemovePlayer(Player player)
        {
            actions.Enqueue(() =>
            {
                GameWorld.Objects.Remove(Players[player]);
                Players.Remove(player);
                foreach(var p in Players)
                {
                    p.Key.Despawn(player.ID);
                }
                Interlocked.Decrement(ref PlayerCount);
            });
        }

        public void PositionUpdate(Player player, Vector3 position, Quaternion orientation)
        {
            actions.Enqueue(() =>
            {
                Players[player].Transform = Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(position);
            });
        }

        public Dictionary<string, GameObject> SpawnedSolars = new Dictionary<string, GameObject>();
        List<GameObject> updatingObjects = new List<GameObject>();
        
        public void SpawnSolar(string nickname, string archetype, string loadout, Vector3 position, Quaternion orientation)
        {
            actions.Enqueue(() =>
            {
                var arch = Server.GameData.GetSolarArchetype(archetype);
                var gameobj = new GameObject(arch, Server.Resources, false, true);
                gameobj.ArchetypeName = archetype;
                gameobj.NetID = GenerateID();
                gameobj.StaticPosition = position;
                gameobj.Transform = Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(position);
                gameobj.Nickname = nickname;
                gameobj.World = GameWorld;
                gameobj.Register(GameWorld.Physics);
                gameobj.CollisionGroups = arch.CollisionGroups;
                GameWorld.Objects.Add(gameobj);
                SpawnedSolars.Add(nickname, gameobj);
                foreach(Player p in Players.Keys)
                    p.SendSolars(SpawnedSolars);
            });
        }

        public void SpawnDebris(string archetype, string part, Matrix4x4 transform, float mass, Vector3 initialForce)
        {
            actions.Enqueue(() =>
            {
                var arch = Server.GameData.GetSolarArchetype(archetype);
                var mdl = ((IRigidModelFile) arch.ModelFile.LoadFile(Server.Resources)).CreateRigidModel(false);
                var newpart = mdl.Parts[part].Clone();
                var newmodel = new RigidModel()
                {
                    Root = newpart,
                    AllParts = new[] { newpart },
                    MaterialAnims = mdl.MaterialAnims,
                    Path = mdl.Path,
                };
                var id = GenerateID();
                var go = new GameObject($"debris{id}", newmodel, Server.Resources, part, mass, false);
                go.NetID = id;
                go.Transform = transform;
                GameWorld.Objects.Add(go);
                updatingObjects.Add(go);
                go.Register(GameWorld.Physics);
                go.PhysicsComponent.Body.Impulse(initialForce);
                //Spawn debris
                foreach (Player p in Players.Keys)
                {
                    p.SpawnDebris(go.NetID, archetype, part, transform, mass);
                }
            });
        }

        public void PartDisabled(GameObject obj, string part)
        {
            foreach (Player p in Players.Keys)
                p.SendDestroyPart(obj.NetID, part);
        }

        public void DeleteSolar(string nickname)
        {
            actions.Enqueue(() =>
            {
                var s = SpawnedSolars[nickname];
                SpawnedSolars.Remove(nickname);
                GameWorld.Objects.Remove(s);
                foreach (Player p in Players.Keys)
                    p.Despawn(s.NetID);
            });
        }

        private TimeSpan noPlayersTime;
        private TimeSpan maxNoPlayers = TimeSpan.FromSeconds(2);
        public bool Update(TimeSpan delta)
        {
            //Avoid locks during Update
            Action act;
            while(actions.Count > 0 && actions.TryDequeue(out act)){ act(); }
            //Update
            GameWorld.Update(delta);
            //Network update tick
            current += delta.TotalSeconds;
            tickTime += delta.TotalSeconds;
            if (tickTime > (LNetConst.MAX_TICK_MS / 1000.0))
                tickTime -= (LNetConst.MAX_TICK_MS / 1000.0);
            var tick = (uint) (tickTime * 1000.0);
            if (current >= UPDATE_RATE) {
                current -= UPDATE_RATE;
                //Send multiplayer updates (less)
                SendPositionUpdates(true, tick);
            }
            //Despawn after 2 seconds of nothing
            if (PlayerCount == 0) {
                noPlayersTime += delta;
                return (noPlayersTime < maxNoPlayers);
            }
            else {
                noPlayersTime = TimeSpan.Zero;
                return true;
            }
        }

        const double UPDATE_RATE = 1 / 25.0;
        double current = 0;
        private double tickTime = 0;
        void GameWorld_PhysicsUpdate(TimeSpan delta)
        {
            SendPositionUpdates(false, 0); //Send single player updates (more)
        }

        //This could do with some work
        void SendPositionUpdates(bool mp, uint tick)
        {
            foreach(var player in Players)
            {
                var tr = player.Value.GetTransform();
                player.Key.Position = Vector3.Transform(Vector3.Zero, tr);
                player.Key.Orientation = tr.ExtractRotation();
            }
            IEnumerable<Player> targets;
            if (mp) {
                targets = Players.Keys.Where(x => x.Client is RemotePacketClient);
            }
            else {
                targets = Players.Keys.Where(x => x.Client is LocalPacketClient);
            }
            foreach (var player in targets)
            {
                List<PackedShipUpdate> ps = new List<PackedShipUpdate>();
                foreach (var otherPlayer in Players.Keys)
                {
                    if (otherPlayer == player) continue;
                    var update = new PackedShipUpdate();
                    update.ID = otherPlayer.ID;
                    update.HasPosition = true;
                    update.Position = otherPlayer.Position;
                    update.HasOrientation = true;
                    update.Orientation = otherPlayer.Orientation;
                    ps.Add(update);
                }
                foreach (var obj in updatingObjects)
                {
                    var update = new PackedShipUpdate();
                    update.ID = obj.NetID;
                    update.HasPosition = true;
                    var tr = obj.GetTransform();
                    update.Position = Vector3.Transform(Vector3.Zero, tr);
                    update.HasOrientation = true;
                    update.Orientation = tr.ExtractRotation();
                    ps.Add(update);
                }
                player.SendUpdate(new ObjectUpdatePacket()
                {
                    Tick = tick,
                    Updates = ps.ToArray()
                });
            }
        }

        public void Finish()
        {
            GameWorld.Dispose();
        }
    }
}
