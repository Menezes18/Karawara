using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    public int xSize = 20, zSize = 20;
    public float height, entropy;
    public Gradient gradient;
    public GameObject box;
    void Start()
    {


            CreateMesh();

    }
    void CreateMesh()
    {
        //passar por todas as coordenadas Z e X
        //definir o noiseX e Y 
        //a posição vai ser sempre x,z e em y arredondado
        for(int z = 0; z <= zSize; z++){
            for(int x = 0; x <= xSize; x++){
                float noiseX = (x/(float)xSize)*entropy;
                float noiseZ = (z/(float)zSize)*entropy;
                float y = Mathf.PerlinNoise(noiseX, noiseZ)* height;
                Vector3 pos = new Vector3(x, Mathf.Round(y), z);
                GameObject obj = Instantiate(box, pos, Quaternion.identity);
                obj.transform.SetParent(this.transform);
                Material m = obj.GetComponent<MeshRenderer>().material;
                m.color = gradient.Evaluate(y/this.height);
            } 
        }
    }

}
