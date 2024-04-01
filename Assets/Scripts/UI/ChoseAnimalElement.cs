using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class ChoseAnimalElement : MonoBehaviour
    {
        private Vector3 initialScale;
        private float maxScaleFactor = 2f;
        public string element;
        public TMP_Text displayElement;
        public ScaleUiTransformation roda;
        public GameObject animal;

        void Start()
        {
            initialScale = transform.localScale;
            StartCoroutine(FadeTextToZeroAlpha());
        }

        public void Update()
        {
            //Distancia do Mouse e a imagem e a imagem pra transformar
            var dist = Vector2.Distance(transform.position, Mouse.current.position.value);
            //Debug.Log(dist);

            // Se distancia for menor que 200 aumenta a imagem
            if (dist <= 200)
            {
                // Calcula a escala proporcional à distância, evitando valores muito grandes
                var newScale = Vector3.Lerp(initialScale, initialScale * maxScaleFactor, 1 - dist / 200);
                transform.localScale = Vector3.Lerp(transform.localScale, newScale, 5f);
            }
            else
            {
                //Volta a imagem ao seu tamanho original
                transform.localScale = Vector3.Lerp(transform.localScale, initialScale, 5f);
            }

            //Garente que quando o mouse eh solto perto da imagem o elemento atual se torna aquele
            if (dist <= 150 && Keyboard.current.tabKey.wasReleasedThisFrame && roda.canTransform == true)
            {
                roda.elementoAtual = element;
                displayElement.text = element;
                roda.canTransform = false;
                StartCoroutine(Transformation());
                StartCoroutine(FadeTextToFullAlpha());
                roda.CurrentAnimal.SetActive(false);
                roda.CurrentAnimal = animal;
                roda.CurrentAnimal.SetActive(true);
            }
        }

        //tempo para transformar de novo
        public IEnumerator Transformation()
        {
            yield return new WaitForSeconds(5f);
            roda.canTransform = true;
            yield return null;
        }

        public IEnumerator FadeTextToFullAlpha()
        {
            displayElement.color = new Color(displayElement.color.r, displayElement.color.g, displayElement.color.b, 0);
            while (displayElement.color.a < 1.0f)
            {
                displayElement.color = new Color(displayElement.color.r, displayElement.color.g, displayElement.color.b, displayElement.color.a + (Time.deltaTime / 2f));
                yield return null;
            }
            StartCoroutine(FadeTextToZeroAlpha());
        }

        public IEnumerator FadeTextToZeroAlpha()
        {
            displayElement.color = new Color(displayElement.color.r, displayElement.color.g, displayElement.color.b, 1);
            while (displayElement.color.a > 0.0f)
            {
                displayElement.color = new Color(displayElement.color.r, displayElement.color.g, displayElement.color.b, displayElement.color.a - (Time.deltaTime / 2f));
                yield return null;
            }
        }

    }
}
