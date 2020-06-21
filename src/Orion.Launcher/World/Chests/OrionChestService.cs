﻿// Copyright (c) 2020 Pryaxis & Orion Contributors
// 
// This file is part of Orion.
// 
// Orion is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Orion is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Orion.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Orion.Core.Events;
using Orion.Core.Events.Packets;
using Orion.Core.Events.World.Chests;
using Orion.Core.Framework;
using Orion.Core.Items;
using Orion.Core.Packets.World.Chests;
using Orion.Core.World.Chests;
using Orion.Launcher.Collections;
using Serilog;

namespace Orion.Launcher.World.Chests
{
    [Binding("orion-chests", Author = "Pryaxis", Priority = BindingPriority.Lowest)]
    internal sealed class OrionChestService : IChestService, IDisposable
    {
        private readonly IEventManager _events;
        private readonly ILogger _log;

        public OrionChestService(IEventManager events, ILogger log)
        {
            Debug.Assert(events != null);
            Debug.Assert(log != null);

            _events = events;
            _log = log;

            // Construct the `Chests` array.
            Chests = new WrappedReadOnlyList<OrionChest, Terraria.Chest?>(
                Terraria.Main.chest, (chestIndex, terrariaChest) => new OrionChest(chestIndex, terrariaChest));

            _events.RegisterHandlers(this, _log);
        }

        public IReadOnlyList<IChest> Chests { get; }

        public void Dispose()
        {
            _events.DeregisterHandlers(this, _log);
        }

        private IChest? FindChest(int x, int y) => Chests.FirstOrDefault(s => s.IsActive && s.X == x && s.Y == y);

        // =============================================================================================================
        // Chest event publishers
        //

        [EventHandler("orion-chests", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnChestOpenPacket(PacketReceiveEvent<ChestOpenPacket> evt)
        {
            ref var packet = ref evt.Packet;
            var chest = FindChest(packet.X, packet.Y);
            if (chest is null)
            {
                return;
            }

            ForwardEvent(evt, new ChestOpenEvent(chest, evt.Sender));
        }

        [EventHandler("orion-chests", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnChestInventoryPacket(PacketReceiveEvent<ChestInventoryPacket> evt)
        {
            ref var packet = ref evt.Packet;
            var itemStack = new ItemStack(packet.Id, packet.StackSize, packet.Prefix);

            ForwardEvent(evt, new ChestInventoryEvent(Chests[packet.ChestIndex], evt.Sender, packet.Slot, itemStack));
        }

        // Forwards `evt` as `newEvt`.
        private void ForwardEvent<TEvent>(Event evt, TEvent newEvt) where TEvent : Event
        {
            _events.Raise(newEvt, _log);
            if (newEvt.IsCanceled)
            {
                evt.Cancel(newEvt.CancellationReason);
            }
        }
    }
}