using UnityEngine;

namespace Chambara
{
    public class ChambaraManager : MonoBehaviour
    {
        [SerializeField] Player playerPrefab;

        [SerializeField] Transform edgePlayerPos0;
        [SerializeField] Transform edgePlayerPos1;

        private void Start()
        {
            StartGame();
        }

        private void StartGame()
        {
            Player player0 = Instantiate(playerPrefab, edgePlayerPos0.position, Quaternion.identity);
            Player player1 = Instantiate(playerPrefab, edgePlayerPos1.position, Quaternion.identity);
            player0.Init(edgePlayerPos0, edgePlayerPos1);
            player1.Init(edgePlayerPos1, edgePlayerPos0);
        }

        
    }
}
