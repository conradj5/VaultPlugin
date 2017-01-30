using Lib_K_Relay;
using Lib_K_Relay.Interface;
using Lib_K_Relay.Networking;
using Lib_K_Relay.Networking.Packets;
using Lib_K_Relay.Networking.Packets.Client;
using Lib_K_Relay.Networking.Packets.DataObjects;
using Lib_K_Relay.Networking.Packets.Server;
using Lib_K_Relay.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vault_Helper
{
    public class Main : IPlugin
    {
        private Dictionary<int, Entity> list = new Dictionary<int, Entity>();
        private bool _inVault = false;

        public string GetAuthor()
        { return "Lime"; }

        public string GetName()
        { return "Vault Helper"; }

        public string GetDescription()
        { return "Quickly swap all or some items into the vault you are over."; }

        public string[] GetCommands()
        { return new string[] { "/vault swap <#>", "/vault drop <#>", "/vault up <#>" }; }

        public void Initialize(Proxy proxy)
        {
            proxy.HookPacket<HelloPacket>(OnHello);
            proxy.HookPacket<UpdatePacket>(OnUpdate);
            proxy.HookPacket<NewTickPacket>(OnNewTick);
            proxy.HookCommand("vault", OnVaultCommand);
        }

        private void OnHello(Client client, HelloPacket packet)
        {
            _inVault = packet.GameId == -5;
            list.Clear();
        }

        private void OnUpdate(Client client, UpdatePacket packet)
        {
            if (!_inVault) return;

            foreach (Entity entity in packet.NewObjs)
            {
                // If the entity is a Vault
                if (entity.ObjectType == 1284)
                {
                    // Add entity to dictionary based on vault ObjectID
                    list[entity.Status.ObjectId] = entity;
                }
            }   
        }

        private void OnNewTick(Client client, NewTickPacket packet)
        {
            if (!_inVault) return;

            foreach (Status stat in packet.Statuses)
            {
                // check if vault changed 
                if (list.ContainsKey(stat.ObjectId))
                {
                    foreach (StatData data in stat.Data)
                    {
                        if (data.Id >= 8 && data.Id <= 15)
                        {
                            // update the value now in the vault
                            list[stat.ObjectId].Status.Data[data.Id - 6].IntValue = data.IntValue;
                        }
                    }
                }
            }
            
        }

        private void OnVaultCommand(Client client, string command, string[] args)
        {
            if (!_inVault)
            {
                client.SendToClient(PluginUtils.CreateOryxNotification("Vault Helper", "This command can only be used in the vault."));
                return;
            }

            int val = 8;
            if (args.Length == 0) return;
            if (args.Length == 1) val = 8;
            else
            {
                if (int.TryParse(args[1].Trim(), out val))
                {
                    if (val < 1 || val > 8)
                    {
                        client.SendToClient(PluginUtils.CreateOryxNotification(
                            "Vault Helper", "You entered an invalid number of items to swap: " + val));
                        return;
                    }
                }
                else
                {
                    client.SendToClient(PluginUtils.CreateOryxNotification(
                        "Vault Helper", "You entered an invalid value for the number of items: " + args[1]));
                    return;
                }
            }

            if (args[0] == "swap") Swap(val, client);
            if (args[0] == "drop") Drop(val, client);
            if (args[0] == "up") Up(val, client);
        }

        private void Swap(int num, Client client)
        {
            for(int idx = 0; idx < num; idx++) {
                InvSwapPacket swap = (InvSwapPacket)Packet.Create(PacketType.INVSWAP);
                
                // Player slot info
                swap.SlotObject1 = new SlotObject();
                swap.SlotObject1.ObjectId = client.PlayerData.OwnerObjectId;
                swap.SlotObject1.SlotId = (byte) (idx + 4);
                swap.SlotObject1.ObjectType = client.PlayerData.Slot[idx + 4];
                
                // Vault slot info
                swap.SlotObject2 = new SlotObject();
                Entity tempVault = FindClosestVault(client);
                swap.SlotObject2.ObjectId = tempVault.Status.ObjectId;
                swap.SlotObject2.SlotId = (byte) idx;
                swap.SlotObject2.ObjectType = tempVault.Status.Data[idx + 2].IntValue;

                if (swap.SlotObject1.ObjectType == -1)
                {
                    if(swap.SlotObject2.ObjectType != -1)
                    {
                        var temp = swap.SlotObject1;
                        swap.SlotObject1 = swap.SlotObject2;
                        swap.SlotObject2 = temp;
                    } else
                    {
                        continue; // Nothing to swap
                    }

                }

                swap.Time = client.Time;
                swap.Position = client.PlayerData.Pos;

                client.SendToServer(swap);
                System.Threading.Thread.Sleep((new Random()).Next(550, 900));
            }
        }

        private void Drop(int num, Client client)
        {
            for (int invIdx = 0, vaultIdx = 0; invIdx < num && vaultIdx < 8; invIdx++)
            {
                InvSwapPacket swap = (InvSwapPacket)Packet.Create(PacketType.INVSWAP);

                // Player slot info
                swap.SlotObject1 = new SlotObject();
                swap.SlotObject1.ObjectId = client.PlayerData.OwnerObjectId;
                swap.SlotObject1.SlotId = (byte)(invIdx + 4);
                swap.SlotObject1.ObjectType = client.PlayerData.Slot[invIdx + 4];

                // Vault slot info
                swap.SlotObject2 = new SlotObject();
                Entity tempVault = FindClosestVault(client);
                swap.SlotObject2.ObjectId = tempVault.Status.ObjectId;
                swap.SlotObject2.SlotId = (byte)vaultIdx;
                swap.SlotObject2.ObjectType = tempVault.Status.Data[vaultIdx + 2].IntValue;

                if (swap.SlotObject1.ObjectType == -1)
                {
                    // no item to drop
                    continue;
                }
                else if (swap.SlotObject2.ObjectType != -1)
                {
                    // vault slot is not empty; try next vault slot
                    vaultIdx++;
                    invIdx--;
                    continue;

                }

                swap.Time = client.Time;
                swap.Position = client.PlayerData.Pos;

                client.SendToServer(swap);
                System.Threading.Thread.Sleep(525);
            }
        }

        private void Up(int num, Client client)
        {
            for (int invIdx = 0, vaultIdx = 0; invIdx < 8 && vaultIdx < num; vaultIdx++)
            {
                InvSwapPacket swap = (InvSwapPacket)Packet.Create(PacketType.INVSWAP);

                // Player slot info
                swap.SlotObject1 = new SlotObject();
                swap.SlotObject1.ObjectId = client.PlayerData.OwnerObjectId;
                swap.SlotObject1.SlotId = (byte)(invIdx + 4);
                swap.SlotObject1.ObjectType = client.PlayerData.Slot[invIdx + 4];

                // Vault slot info
                swap.SlotObject2 = new SlotObject();
                Entity tempVault = FindClosestVault(client);
                swap.SlotObject2.ObjectId = tempVault.Status.ObjectId;
                swap.SlotObject2.SlotId = (byte)vaultIdx;
                swap.SlotObject2.ObjectType = tempVault.Status.Data[vaultIdx + 2].IntValue;

                if (swap.SlotObject2.ObjectType == -1)
                {
                    // no item to pick up
                    continue;
                }
                else if (swap.SlotObject1.ObjectType != -1)
                {
                    // inv slot is taken; try next one
                    vaultIdx--;
                    invIdx++;
                    continue;

                }

                swap.Time = client.Time;
                swap.Position = client.PlayerData.Pos;

                client.SendToServer(swap);
                System.Threading.Thread.Sleep(525);
            }

        }

        private Entity FindClosestVault(Client client)
        {
            Entity result = list.First().Value;
            float minX = float.MaxValue, minY = float.MaxValue;
            foreach (Entity temp in list.Values)
            {
                if (Math.Abs(temp.Status.Position.X - client.PlayerData.Pos.X) < minX ||
                    Math.Abs(temp.Status.Position.Y - client.PlayerData.Pos.Y) < minY)
                {
                    minX = Math.Abs(temp.Status.Position.X - client.PlayerData.Pos.X);
                    minY = Math.Abs(temp.Status.Position.Y - client.PlayerData.Pos.Y);
                    result = temp;
                }
            }
            return result;
        }
    }
}
