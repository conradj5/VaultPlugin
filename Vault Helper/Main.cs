using Lib_K_Relay;
using Lib_K_Relay.Interface;
using Lib_K_Relay.Networking;
using Lib_K_Relay.Networking.Packets;
using Lib_K_Relay.Networking.Packets.Client;
using Lib_K_Relay.Networking.Packets.DataObjects;
using Lib_K_Relay.Networking.Packets.Server;
using Lib_K_Relay.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault_Helper
{
    public class Main : IPlugin
    {
        private bool _inVault = false;
        Dictionary<int, Entity> list = new Dictionary<int, Entity>();

        public string GetAuthor()
        { return "Lime"; }

        public string GetName()
        { return "Vault Helper"; }

        public string GetDescription()
        { return "Quickly swap all or some items into the vault you are over."; }

        public string[] GetCommands()
        { return new string[] { "/vault all", "/vault #" }; }

        public void Initialize(Proxy proxy)
        {
            proxy.HookPacket<HelloPacket>(OnHello);
            proxy.HookPacket<UpdatePacket>(OnUpdate);
            proxy.HookPacket<NewTickPacket>(OnNewTick);

            proxy.HookCommand("vault", OnVaultCommand);
        }

        private void OnHello(Client client, HelloPacket packet)
        {
            _inVault = packet.GameId == -5 ? true : false;
            list.Clear();
        }

        private void OnUpdate(Client client, UpdatePacket packet)
        {
            if (_inVault)
            {
                foreach (Entity entity in packet.NewObjs)
                {
                    // If the entity is a Vault
                    if (entity.ObjectType == 1284)
                    {
                        // Add entity to dictionary<ID,entity>
                        list[entity.Status.ObjectId] = entity;
                    }
                }
            }
        }

        private void OnNewTick(Client client, NewTickPacket packet)
        {
            foreach(Status stat in packet.Statuses)
            {
                // check if change involves vault
                if (list.ContainsKey(stat.ObjectId))
                {
                    foreach(StatData data in stat.Data)
                    {
                        if (data.Id >= 8 && data.Id <= 15)
                        {
                            // update the objectid for the moved item
                            list[stat.ObjectId].Status.Data[data.Id - 6].IntValue = data.IntValue;
                        }
                    }
                }
            }
        }
        
        private void OnVaultCommand(Client client, string command, string[] args)
        {
            int val = 0;
            if (args.Length == 0) return;
            if (args[0] == "all") Swap(8, client);
            else if (int.TryParse(args[0], out val)) {
                if (val > 0 && val < 9)
                {
                    Swap(val, client);
                }
                else
                {
                    client.SendToClient(PluginUtils.CreateOryxNotification(
                    "Vault Helper", "You entered an invalid number of items to swap: " + val));
                }
            }
        }

        private void Swap(int num, Client client)
        {
            // find correct vault from list
            for(int x = 0; x < num; x++) {
                InvSwapPacket swap = (InvSwapPacket)Packet.Create(PacketType.INVSWAP);
                
                // Player slot info
                swap.SlotObject1 = new SlotObject();
                swap.SlotObject1.ObjectId = client.PlayerData.OwnerObjectId;
                swap.SlotObject1.SlotId = (byte) (x + 4);
                swap.SlotObject1.ObjectType = client.PlayerData.Slot[x + 4];
                
                // Vault slot info
                swap.SlotObject2 = new SlotObject();
                Entity tempVault = FindClosestVault(client);
                swap.SlotObject2.ObjectId = tempVault.Status.ObjectId;
                swap.SlotObject2.SlotId = (byte) x;
                swap.SlotObject2.ObjectType = tempVault.Status.Data[x + 2].IntValue;

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

                //_waitForSuccess = true;
                //Console.WriteLine("Sending {0}\nMade: {1}", x, swap.ToString());
                client.SendToServer(swap);
                System.Threading.Thread.Sleep(525);
                //while (_waitForSuccess) ;
                //Console.WriteLine("success");
            }
        }

        private Entity FindClosestVault(Client client)
        {
            Entity tempVault = list.First().Value;
            float minX = float.MaxValue, minY = float.MaxValue;
            foreach (Entity temp in list.Values)
            {
                if (Math.Abs(temp.Status.Position.X - client.PlayerData.Pos.X) < minX ||
                    Math.Abs(temp.Status.Position.Y - client.PlayerData.Pos.Y) < minY)
                {
                    minX = Math.Abs(temp.Status.Position.X - client.PlayerData.Pos.X);
                    minY = Math.Abs(temp.Status.Position.Y - client.PlayerData.Pos.Y);
                    tempVault = temp;
                }
            }
            //Console.WriteLine("Chose vault {0}: minX: {1}\tminY: {2}", tempVault.Status.ObjectId, minX, minY);
            return tempVault;
        }
    }
}
