using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
public class MainMenue : MonoBehaviour
{
    public TMP_Text titleText;
    public float AlphaChangeSpeed = 1.0f;
    public float Alpha = 0.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        titleText.color = new Color(0.68f, 0.85f, 0.9f, Alpha);
    }

    // Update is called once per frame
    void Update()
    {
        ChangeAlpha();
         if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            int currentScene = SceneManager.GetActiveScene().buildIndex;

            if (currentScene == 6)
            {
                SceneManager.LoadScene(1);
            }
            else
            {
                SceneManager.LoadScene(currentScene + 1);
            }
        }
    }

    void ChangeAlpha()
    {
        Alpha = Mathf.PingPong(Time.time * AlphaChangeSpeed, 1.0f);
        titleText.color = new Color(0.68f, 0.85f, 0.9f, Alpha);
    }
}
