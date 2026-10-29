using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace RPGKarawara
{
    public class WorldItemDatabase : MonoBehaviour
    {
        public static WorldItemDatabase Instance;

        public WeaponItem unarmedWeapon;

        public GameObject pickUpItemPrefab;

        [Header("Weapons")]
        [SerializeField] List<WeaponItem> weapons = new List<WeaponItem>();

        [Header("Spells")]
        [SerializeField] List<SpellItem> spells = new List<SpellItem>();

        //  A LIST OF EVERY ITEM WE HAVE IN THE GAME
        [Header("Items")]
        private List<Item> items = new List<Item>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            //  ADD ALL OF OUR WEAPONS TO THE LIST OF ITEMS
            foreach (var weapon in weapons)
            {
                items.Add(weapon);
            }
            foreach (var item in spells)
            {
                items.Add(item);
            }

            // IDs are authored content. Loading a catalog must not mutate definitions.
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public Item GetItemByID(int ID)
        {
            return items.FirstOrDefault(item => item.itemID == ID);
        }

        public WeaponItem GetWeaponByID(int ID)
        {
            return weapons.FirstOrDefault(weapon => weapon.itemID == ID);
        }
        
        public SpellItem GetSpellByID(int ID)
        {
            return spells.FirstOrDefault(item => item.itemID == ID);
        }
    }
}
