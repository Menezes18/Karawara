using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class PlayerUIHudManager : MonoBehaviour
    {
        [Header("STAT BARS")]
        [SerializeField] UI_StatBar healthBar;
        [SerializeField] UI_StatBar staminaBar;

        [Header("QUICK SLOTS")]
        [SerializeField] Image rightWeaponQuickSlotIcon;
        [SerializeField] Image leftWeaponQuickSlotIcon;

        public void RefreshHUD()
        {
            healthBar.gameObject.SetActive(false);
            healthBar.gameObject.SetActive(true);
            staminaBar.gameObject.SetActive(false);
            staminaBar.gameObject.SetActive(true);
        }

        public void SetNewHealthValue(int oldValue, int newValue)
        {
            healthBar.SetStat(newValue);
        }

        public void SetMaxHealthValue(int maxhealth)
        {
            healthBar.SetMaxStat(maxhealth);
        }

        public void SetNewStaminaValue(float oldValue, float newValue)
        {
            staminaBar.SetStat(Mathf.RoundToInt(newValue));
        }

        public void SetMaxStaminaValue(int maxStamina)
        {
            staminaBar.SetMaxStat(maxStamina);
        }

        public void SetRightWeaponQuickSlotIcon(int weaponID)
        {
            //1. Method one, DIRECTLY reference the right weapon in the hand of the player
            //Pros: It's super straight forward
            //Cons: If you forget to call this function AFTER you've loaded your weapons first, it will give you an error
            //Example: You load a previously saved game, you go to reference the weapons upon loading UI but they arent instantiated yet
            //Final Notes: This method is perfectly fine if you remember your order of operations

            //2. Method two, REQUIRE an item ID of the weapon, fetch the weapon from our database and use it to get the weapon items icon
            //Pros: Since you always save the current weapons ID, we dont need to wait to get it from the player we could get it before hand if required
            //Cons: It's not as direct
            //Final Notes: This method is great if you don't want to remember another oder of operations

            //  IF THE DATABASE DOES NOT CONTAIN A WEAPON MATCHING THE GIVEN I.D, RETURN

            WeaponItem weapon = WorldItemDatabase.Instance.GetWeaponByID(weaponID);

            if (weapon == null)
            {
                Debug.Log("ITEM IS NULL");
                rightWeaponQuickSlotIcon.enabled = false;
                rightWeaponQuickSlotIcon.sprite = null;
                return;
            }

            if (weapon.itemIcon == null)
            {
                Debug.Log("ITEM HAS NO ICON");
                rightWeaponQuickSlotIcon.enabled = false;
                rightWeaponQuickSlotIcon.sprite = null;
                return;
            }

            //  THIS IS WHERE YOU WOULD CHECK TO SEE IF YOU MEET THE ITEMS REQUIREMENTS IF YOU WANT TO CREATE THE WARNING FOR NOT BEING ABLE TO WIELD IT IN THE UI

            rightWeaponQuickSlotIcon.sprite = weapon.itemIcon;
            rightWeaponQuickSlotIcon.enabled = true;
        }

        public void SetLeftWeaponQuickSlotIcon(int weaponID)
        {
            //1. Method one, DIRECTLY reference the right weapon in the hand of the player
            //Pros: It's super straight forward
            //Cons: If you forget to call this function AFTER you've loaded your weapons first, it will give you an error
            //Example: You load a previously saved game, you go to reference the weapons upon loading UI but they arent instantiated yet
            //Final Notes: This method is perfectly fine if you remember your order of operations

            //2. Method two, REQUIRE an item ID of the weapon, fetch the weapon from our database and use it to get the weapon items icon
            //Pros: Since you always save the current weapons ID, we dont need to wait to get it from the player we could get it before hand if required
            //Cons: It's not as direct
            //Final Notes: This method is great if you don't want to remember another oder of operations

            //  IF THE DATABASE DOES NOT CONTAIN A WEAPON MATCHING THE GIVEN I.D, RETURN

            WeaponItem weapon = WorldItemDatabase.Instance.GetWeaponByID(weaponID);

            if (weapon == null)
            {
                Debug.Log("ITEM IS NULL");
                leftWeaponQuickSlotIcon.enabled = false;
                leftWeaponQuickSlotIcon.sprite = null;
                return;
            }

            if (weapon.itemIcon == null)
            {
                Debug.Log("ITEM HAS NO ICON");
                leftWeaponQuickSlotIcon.enabled = false;
                leftWeaponQuickSlotIcon.sprite = null;
                return;
            }

            //  THIS IS WHERE YOU WOULD CHECK TO SEE IF YOU MEET THE ITEMS REQUIREMENTS IF YOU WANT TO CREATE THE WARNING FOR NOT BEING ABLE TO WIELD IT IN THE UI

            leftWeaponQuickSlotIcon.sprite = weapon.itemIcon;
            leftWeaponQuickSlotIcon.enabled = true;
        }
    }
}
