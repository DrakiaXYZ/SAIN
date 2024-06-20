﻿using EFT;
using EFT.InventoryLogic;
using SAIN.SAINComponent;
using SAIN.SAINComponent.Classes.Decision;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static EFT.InventoryLogic.Weapon;
using static RootMotion.FinalIK.GenericPoser;

namespace SAIN.Components.BotComponentSpace.Classes
{
    public class ReloadClass : SAINBase, ISAINClass
    {
        public BotMagazineWeapon ActiveMagazineWeapon { get; private set; }
        public EquipmentSlot ActiveEquipmentSlot => _weaponManager.Selector.EquipmentSlot;
        public Weapon CurrentWeapon => _weaponManager?.CurrentWeapon;

        public readonly Dictionary<EquipmentSlot, BotMagazineWeapon> BotMagazineWeapons = new Dictionary<EquipmentSlot, BotMagazineWeapon> ();

        public ReloadClass(BotComponent bot) : base(bot)
        {
            _weaponManager = Bot.BotOwner.WeaponManager;
            findWeapons();
        }

        private void findWeapons()
        {
            foreach (EquipmentSlot slot in _weapSlots)
            {
                Item item = Bot.Player.Equipment.GetSlot(slot).ContainedItem;
                if (item != null && 
                    item is Weapon weapon && 
                    isMagFed(weapon.ReloadMode))
                {
                    BotMagazineWeapons.Add(slot, new BotMagazineWeapon(weapon, Bot));
                }
            }
        }

        public void Init()
        {
            _weaponManager.Selector.OnActiveEquipmentSlotChanged += weaponChanged;
        }

        private void weaponChanged(EquipmentSlot slot)
        {
            _weaponChanged = true;
        }

        public void Update()
        {
            checkWeapChanged();
        }

        private void checkWeapChanged()
        {
            if (_weaponChanged)
            {
                if (ActiveEquipmentSlot == EquipmentSlot.Scabbard)
                {
                    _weaponChanged = false;
                    return;
                }

                Weapon weapon = CurrentWeapon;
                if (weapon == null)
                {
                    return;
                }

                ActiveMagazineWeapon = findOrCreateMagWeapon(weapon);
                if (ActiveMagazineWeapon != null)
                {
                    Logger.LogDebug("Found Mag Weapon.");
                }
                _weaponChanged = false;
            }
        }

        private static bool isMagFed(EReloadMode reloadMode)
        {
            switch (reloadMode)
            {
                case EReloadMode.ExternalMagazine:
                case EReloadMode.ExternalMagazineWithInternalReloadSupport:
                    return true;

                default:
                    return false;
            }
        }

        private BotMagazineWeapon findOrCreateMagWeapon(Weapon weapon)
        {
            if (!isMagFed(weapon.ReloadMode))
            {
                return null;
            }

            EquipmentSlot activeSlot = ActiveEquipmentSlot;
            if (!BotMagazineWeapons.TryGetValue(activeSlot, out BotMagazineWeapon magazineWeapon))
            {
                BotMagazineWeapons.Add(activeSlot, new BotMagazineWeapon(weapon, Bot));
                return BotMagazineWeapons[activeSlot];
            }

            if (magazineWeapon.Weapon != weapon)
            {
                BotMagazineWeapons[activeSlot] = new BotMagazineWeapon(weapon, Bot);
            }
            return BotMagazineWeapons[activeSlot];
        }

        public void Dispose()
        {
            if (_weaponManager.Selector != null)
            {
                _weaponManager.Selector.OnActiveEquipmentSlotChanged -= weaponChanged;
            }
        }

        private static EquipmentSlot[] _weapSlots = 
        { 
            EquipmentSlot.FirstPrimaryWeapon, 
            EquipmentSlot.SecondPrimaryWeapon, 
            EquipmentSlot.Holster 
        };

        private bool _weaponChanged = true;
        private readonly BotWeaponManager _weaponManager;
    }

    public class BotMagazineWeapon
    {
        public BotMagazineWeapon(Weapon weapon, BotComponent bot)
        {
            Weapon = weapon;
            _inventoryController = bot.Player.GClass2761_0;
            _weaponManager = bot.BotOwner.WeaponManager;
            Update();
        }

        public void Init(BotOwner botOwner)
        {
            if (botOwner == null)
                return;

            if (botOwner.ItemTaker != null)
                botOwner.ItemTaker.OnItemTaken += checkItemTaken;

            if (botOwner.ItemDropper != null)
                botOwner.ItemDropper.OnItemDrop += checkItemDropped;
        }

        public void Dispose(BotOwner botOwner)
        {
            Magazines.Clear();

            if (botOwner == null)
                return;

            if (botOwner.ItemTaker != null)
                botOwner.ItemTaker.OnItemTaken -= checkItemTaken;

            if (botOwner.ItemDropper != null)
                botOwner.ItemDropper.OnItemDrop -= checkItemDropped;

        }

        private void checkItemTaken(Item item, IPlayer player)
        {
            _needToRecheck = true;
        }

        private void checkItemDropped(Item item)
        {
            _needToRecheck = true;
        }

        private bool findMags()
        {
            Weapon weapon = Weapon;
            if (weapon == null)
            {
                Logger.LogDebug("Weapon Null");
                return false;
            }
            return getNonActiveMagazines() > 0;
        }

        private bool _magsFound;
        private bool _needToRecheck = true;

        public void Update()
        {
            if (_needToRecheck && _nextCheckTime < Time.time)
            {
                _nextCheckTime = Time.time + 1f;
                _needToRecheck = false;
                _magsFound = findMags();
                checkMagAmmoStatus();
            }
        }

        private float _nextCheckTime;

        private int getNonActiveMagazines()
        {
            if (getActiveMagazine() == null)
            {
                Logger.LogDebug("Active Magazine is Null!");
                return 0;
            }
            Magazines.Clear();
            _inventoryController.GetReachableItemsOfTypeNonAlloc<MagazineClass>(Magazines, new Func<MagazineClass, bool>(_refill.canAccept));
            return Magazines.Count;
        }

        public bool TryRefillAllMags()
        {
            return TryRefillMags(-1);
        }

        public bool TryRefillMags(int count)
        {
            if (!IsMagazineFed)
            {
                Logger.LogDebug($"weapon doesn't use mags!");
                return false;
            }
            if (!_magsFound)
            {
                Logger.LogDebug($"no magazines found!");
                return false;
            }
            Weapon weapon = Weapon;
            if (weapon == null)
            {
                Logger.LogDebug("Weapon Null");
                return false;
            }
            return refillMagsInList(Magazines, weapon, count);
        }

        public readonly bool IsMagazineFed;

        private Slot getActiveMagazine()
        {
            Slot slot = Weapon?.GetMagazineSlot();
            _refill.magazineSlot = slot;
            return slot;
        }

        private bool refillMagsInList(List<MagazineClass> list, Weapon weapon, int numberToRefill = -1)
        {
            int refilled = 0;

            foreach (MagazineClass mag in list)
            {
                if (mag == null) continue;

                if (mag.Count < mag.MaxCount)
                {
                    int startCount = mag.Count;
                    _weaponManager.Reload.method_2(weapon, mag);
                    
                    if (mag.Count < mag.MaxCount)
                    {
                        Logger.LogDebug($"Mag Count still below max after refill. Current: {mag.Count} : Max {mag.MaxCount} : count before attempt: {startCount}");
                    }

                    refilled++;
                    if (numberToRefill < 0)
                    {
                        continue;
                    }
                    if (refilled >= numberToRefill)
                    {
                        break;
                    }
                }
            }
            return refilled > 0;
        }

        private void checkMagAmmoStatus()
        {
            int fullMags = 0;
            int partialMags = 0;
            int emptyMags = 0;

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"Checking Mags...");

            int count = 0;
            foreach (MagazineClass mag in Magazines)
            {
                if (mag == null) continue;

                count++;
                float ratio = getAmmoRatio(mag);
                stringBuilder.AppendLine($"Mag [{count}] : Count [{mag.Count}] : Capacity [{mag.MaxCount}] : Ratio [{ratio}]");

                if (ratio <= 0)
                {
                    emptyMags++;
                    continue;
                }
                if (ratio < 1f)
                {
                    partialMags++;
                    continue;
                }
                fullMags++;
            }

            stringBuilder.AppendLine($"Full [{fullMags}] : Partial [{partialMags}] : Empty [{emptyMags}]");
            Logger.LogDebug(stringBuilder.ToString());

            FullMagazineCount = fullMags;
            PartialMagazineCount = partialMags;
            EmptyMagazineCount = emptyMags;
        }

        public bool CheckAnyMagHasAmmo(float ratioToCheck)
        {
            foreach (MagazineClass mag in Magazines)
            {
                float ammoRatio = getAmmoRatio(mag);
                if (ammoRatio >= ratioToCheck)
                {
                    return true;
                }
            }

            return false;
        }

        private float getAmmoRatio(MagazineClass magazine)
        {
            if (magazine == null) return 0.0f;

            return (float)magazine.Count / (float)magazine.MaxCount;
        }

        public int FullMagazineCount { get; private set; }
        public int PartialMagazineCount { get; private set; }
        public int EmptyMagazineCount { get; private set; }

        public readonly Weapon Weapon;
        public readonly List<MagazineClass> Magazines = new List<MagazineClass>();
        private readonly BotWeaponManager _weaponManager;
        private readonly InventoryControllerClass _inventoryController;
        private MagRefillClass _refill = new MagRefillClass();
    }

    public class MagRefillClass
    {
        public bool canAccept(MagazineClass mag)
        {
            return this.magazineSlot.CanAccept(mag);
        }

        public Slot magazineSlot;
    }
}