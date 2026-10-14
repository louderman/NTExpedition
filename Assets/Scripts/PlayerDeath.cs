using System.Runtime.CompilerServices;
using TMPro;                                                                                                                                                                                                                                                                           
using UnityEngine;                                                                                                                                                                                                                                                                     
using UnityEngine.SceneManagement;                                                                                                                                                                                                                                                     
                                                                                                                                                                                                                                                                                            
public class PlayerDeath : MonoBehaviour                                                                                                                                                                                                                                               
{
    [SerializeField] private GameObject gameOverPanel;                                                                                                                                                                                                                                 
    [SerializeField] private TMP_Text finalScoreText;                                                                                                                                                                                                                                  
    [SerializeField] private ScoreManager scoreManager;

    [SerializeField] private Health playerHealth;

    void Awake()
    {
        playerHealth = GetComponent<Health>();
    }          

    void OnEnable()
    {
        playerHealth.Died += HandleDeath;
    }

    void OnDisable()
    {
        playerHealth.Died -= HandleDeath;
    }

    void HandleDeath()
    {
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) { flow.EndRun(); return; }
        finalScoreText.text = $"Score: {ScoreManager.Instance.CurrentScore}";                                                                                                                                                                                                                                                
        gameOverPanel.SetActive(true);                                                                                                                                                                                                                                             
        Time.timeScale = 0f;    
    }                                                                                                                                                                                                                                                                              
                                                                                                                                                                                                                                                                                            
    public void RestartGame()                                                                                                                                                                                                                                                          
    {                                                                                                                                                                                                                                                                                  
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) { flow.RestartRun(); return; }
        ScoreManager.Instance?.ResetCurrentScore();
        Time.timeScale = 1f;                                                                                                                                                                                                                                                           
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    
    }                                                                                                                                                                                                                                                                                  
}