using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara{
    public class NPCController : MonoBehaviour{
        public Transform[] patrolPoints;
        public float detectionRadius = 5f;
        public float npcInteractionRadius = 2f;
        public string[] dialogues;

        private NavMeshAgent agent;
        private int currentPatrolIndex;
        private GameObject player;
        private bool isInteracting;
        private bool isTalkingToNPC;

        void Start(){
            agent = GetComponent<NavMeshAgent>();
            player = GameObject.FindGameObjectWithTag("Player");
            currentPatrolIndex = 0;
            GoToNextPatrolPoint();
        }

        void Update(){
            if (isInteracting || isTalkingToNPC)
                return;

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);


            if (distanceToPlayer <= detectionRadius){
                LookAtPlayer();
                StartDialogue();
            }
            else if (!agent.pathPending && agent.remainingDistance < 0.5f){
                GoToNextPatrolPoint();
            }


            Collider[] nearbyNPCs = Physics.OverlapSphere(transform.position, npcInteractionRadius);
            foreach (Collider col in nearbyNPCs){
                if (col.CompareTag("NPC") && col.gameObject != this.gameObject){
                    if (Random.Range(0, 100) < 20) // 20% de chance de iniciar uma conversa
                    {
                        StartCoroutine(TalkToNPC(col.gameObject));
                    }
                }
            }
        }

        void GoToNextPatrolPoint(){
            if (patrolPoints.Length == 0)
                return;

            agent.destination = patrolPoints[currentPatrolIndex].position;
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }

        void LookAtPlayer(){
            Vector3 direction = (player.transform.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        void StartDialogue(){
            isInteracting = true;
            string message = dialogues[Random.Range(0, dialogues.Length)];
            DialogueSystemNPC.instance.ShowMessage(message);
            Invoke(nameof(EndInteraction), 3f);
        }

        void EndInteraction(){
            isInteracting = false;
        }

        System.Collections.IEnumerator TalkToNPC(GameObject otherNPC){
            isTalkingToNPC = true;
            LookAtPlayer(); // Olha para o outro NPC

            // Simula uma conversa
            string[] conversation ={ "Olá!", "Como vai?", "Que dia bonito!" };
            foreach (string line in conversation){
                DialogueSystemNPC.instance.ShowMessage(line);
                yield return new WaitForSeconds(2f);
            }

            isTalkingToNPC = false;
        }
    }
}