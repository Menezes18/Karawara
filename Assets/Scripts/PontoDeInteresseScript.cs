using TMPro; 
using UnityEngine;
using UnityEngine.UI;

public class IndicadorObjetivoScript : MonoBehaviour
{
    public static IndicadorObjetivoScript indicador;
    public Transform jogador;
    public Transform objetivo;
    public RectTransform canvasRect;
    public TMP_Text textoDistancia;
    private RectTransform indicadorRect;

    void Start()
    {
        indicador = this;
        if (jogador == null)
        {
            jogador = GameObject.FindGameObjectWithTag("Player").transform;
        }

        indicadorRect = GetComponent<RectTransform>();
    }

    void Update()
    {
        Vector3 screenPoint = Camera.main.WorldToViewportPoint(objetivo.position);

        bool objetivoAFrente = screenPoint.z > 0;

        Vector3 direcao = (objetivo.position - jogador.position).normalized;

        float angle = Mathf.Atan2(direcao.z, direcao.x) * Mathf.Rad2Deg;
        indicadorRect.rotation = Quaternion.Euler(0, 0, -angle + 90);

        if (objetivoAFrente && screenPoint.x >= 0 && screenPoint.x <= 1 && screenPoint.y >= 0 && screenPoint.y <= 1)
        {
            screenPoint.x = Mathf.Clamp(screenPoint.x, 0.05f, 0.95f);
            screenPoint.y = Mathf.Clamp(screenPoint.y, 0.05f, 0.95f);
        }
        else
        {

            screenPoint = CalcularPosicaoBordaTela(screenPoint);
        }

        Vector3 indicadorPosicao = CalcularPosicaoIndicador(screenPoint);
        indicadorRect.position = indicadorPosicao;

        textoDistancia.rectTransform.position = indicadorPosicao + new Vector3(0, -30, 0);

        float distancia = Vector3.Distance(objetivo.position, jogador.position);
        textoDistancia.text = distancia.ToString("F1") + "m";

        indicadorRect.gameObject.SetActive(true);
        textoDistancia.gameObject.SetActive(true);
    }

    Vector3 CalcularPosicaoBordaTela(Vector3 screenPoint)
    {
        Vector3 screenCenter = new Vector3(0.5f, 0.5f);

        Vector3 direcaoDoCentroAoObjetivo = screenPoint - screenCenter;
        direcaoDoCentroAoObjetivo.z = 0;
        direcaoDoCentroAoObjetivo.Normalize();

        float m = direcaoDoCentroAoObjetivo.y / direcaoDoCentroAoObjetivo.x;

        Vector3 pontoNaBorda = screenCenter;

        if (direcaoDoCentroAoObjetivo.x > 0)
        {
            pontoNaBorda.x = 1f;
            pontoNaBorda.y = screenCenter.y + m * (pontoNaBorda.x - screenCenter.x);
        }
        else if (direcaoDoCentroAoObjetivo.x < 0)
        {
            pontoNaBorda.x = 0f;
            pontoNaBorda.y = screenCenter.y + m * (pontoNaBorda.x - screenCenter.x);
        }
        else
        {
            pontoNaBorda.x = screenCenter.x;
            pontoNaBorda.y = direcaoDoCentroAoObjetivo.y > 0 ? 1f : 0f;
            return pontoNaBorda;
        }

        if (pontoNaBorda.y > 1f || pontoNaBorda.y < 0f)
        {
            if (direcaoDoCentroAoObjetivo.y > 0)
            {
                pontoNaBorda.y = 1f;
                pontoNaBorda.x = screenCenter.x + (pontoNaBorda.y - screenCenter.y) / m;
            }
            else
            {
                pontoNaBorda.y = 0f;
                pontoNaBorda.x = screenCenter.x + (pontoNaBorda.y - screenCenter.y) / m;
            }
        }

        pontoNaBorda.x = Mathf.Clamp(pontoNaBorda.x, 0.05f, 0.95f);
        pontoNaBorda.y = Mathf.Clamp(pontoNaBorda.y, 0.05f, 0.95f);

        return pontoNaBorda;
    }

    Vector3 CalcularPosicaoIndicador(Vector3 screenPoint)
    {
        Vector2 canvasPos;
        canvasPos.x = (screenPoint.x * canvasRect.sizeDelta.x) - (canvasRect.sizeDelta.x * 0.5f);
        canvasPos.y = (screenPoint.y * canvasRect.sizeDelta.y) - (canvasRect.sizeDelta.y * 0.5f);

        return canvasRect.transform.TransformPoint(canvasPos);
    }
}
