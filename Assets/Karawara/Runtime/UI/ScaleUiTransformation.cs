using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

    public class ScaleUiTransformation : MonoBehaviour
    {
       [SerializeField] private GameObject Roda;
        public string elementoAtual;
        public bool canTransform = true;
        public GameObject CurrentAnimal;

        public void Start()
        {
            StartCoroutine(Opening());
            
        }

        public void Update()
        {
            if (RPGKarawara.PlayerInputManager.instance == null || !RPGKarawara.PlayerInputManager.instance.CanRouteGameplay) return;
            if (RPGKarawara.PlayerInputManager.instance.Router.WasPressed(Key.Tab))
            {
                StartCoroutine(Closing());
            }
            else if (RPGKarawara.PlayerInputManager.instance.Router.WasReleased(Key.Tab))
            {
                StartCoroutine(Opening());
            }
        }

        public IEnumerator Opening()
        {
            float elapsedTime = 0;
            float waitTime = 0.2f;
            Vector3 startingScale = Roda.transform.localScale;
            Vector3 targetScale = new Vector3(0.01f, 0.01f, 0.01f);

            while (elapsedTime < waitTime)
            {
                Roda.transform.localScale = Vector3.Lerp(startingScale, targetScale, (elapsedTime / waitTime));
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Roda.transform.localScale = targetScale;
        }

        public IEnumerator Closing()
        {
            float elapsedTime = 0;
            float waitTime = 0.2f;
            Vector3 startingScale = Roda.transform.localScale;
            Vector3 targetScale = new Vector3(1f, 1f, 1f);

            while (elapsedTime < waitTime)
            {
                Roda.transform.localScale = Vector3.Lerp(startingScale, targetScale, (elapsedTime / waitTime));
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Roda.transform.localScale = targetScale;
        }
    }


