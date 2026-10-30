using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGKarawara
{
    public class UIProximity : MonoBehaviour
    {
        public GameObject uiElement;
        public string textGUI = "Press 'E' to interact";
        public TextMeshProUGUI uiProximityText;
        public float detectionRadius = 3f;

        private Transform player;

        void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            uiElement.SetActive(false);
            uiProximityText = uiElement.GetComponent<TextMeshProUGUI>();

            if (uiProximityText != null)
            {
                uiProximityText.text = textGUI;
            }
        }

        void Update()
        {
            bool isPlayerNearby = Vector3.Distance(player.position, transform.position) <= detectionRadius;
            uiElement.SetActive(isPlayerNearby);

            if (isPlayerNearby && uiProximityText != null)
            {
                uiProximityText.text = textGUI;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}