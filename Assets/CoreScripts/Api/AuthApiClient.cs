using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class AuthApiClient : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:5081";

    [Header("UI Inputs")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField fullNameInput;

    [Header("UI Groups")]
    [SerializeField] private GameObject emailGroup;
    [SerializeField] private GameObject fullNameGroup;

    [Header("UI Text")]
    [SerializeField] private TMP_Text usernameLabel;
    [SerializeField] private TMP_Text emailLabel;
    [SerializeField] private TMP_Text passwordLabel;
    [SerializeField] private TMP_Text fullNameLabel;
    [SerializeField] private TMP_Text loginButtonText;
    [SerializeField] private TMP_Text registerButtonText;
    [SerializeField] private TMP_Text messageText;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SumMenu";

    private bool isRegisterMode = false;
    private Coroutine authCoroutine;

    private void Start()
    {
        SetLoginMode();

        if (passwordInput != null)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
        }
    }

    public void OnLoginButtonClicked()
    {
        if (isRegisterMode)
        {
            SetLoginMode();
            return;
        }

        Login();
    }

    public void OnRegisterButtonClicked()
    {
        if (!isRegisterMode)
        {
            SetRegisterMode();
            return;
        }

        Register();
    }

    public void SetLoginMode()
    {
        isRegisterMode = false;

        if (emailGroup != null)
            emailGroup.SetActive(false);

        if (fullNameGroup != null)
            fullNameGroup.SetActive(false);

        if (usernameLabel != null)
            usernameLabel.text = "Введите свой никнейм или почту";

        if (passwordLabel != null)
            passwordLabel.text = "Введите свой пароль";

        if (loginButtonText != null)
            loginButtonText.text = "Войти";

        if (registerButtonText != null)
            registerButtonText.text = "Регистрация";

        if (emailInput != null)
            emailInput.text = "";

        if (fullNameInput != null)
            fullNameInput.text = "";

        ShowMessage("");
    }

    public void SetRegisterMode()
    {
        isRegisterMode = true;

        if (emailGroup != null)
            emailGroup.SetActive(true);

        if (fullNameGroup != null)
            fullNameGroup.SetActive(true);

        if (usernameLabel != null)
            usernameLabel.text = "Введите свой никнейм";

        if (emailLabel != null)
            emailLabel.text = "Введите свою почту";

        if (passwordLabel != null)
            passwordLabel.text = "Введите свой пароль";

        if (fullNameLabel != null)
            fullNameLabel.text = "Введите ФИО";

        if (loginButtonText != null)
            loginButtonText.text = "Назад";

        if (registerButtonText != null)
            registerButtonText.text = "Зарегистрироваться";

        ShowMessage("");
    }

    public void Register()
    {
        string username = usernameInput != null ? usernameInput.text.Trim() : "";
        string email = emailInput != null ? emailInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text.Trim() : "";
        string fullName = fullNameInput != null ? fullNameInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowMessage("Введите никнейм");
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowMessage("Введите почту");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Введите пароль");
            return;
        }

        RegisterRequest request = new RegisterRequest
        {
            username = username,
            email = email,
            password = password,
            full_name = fullName
        };

        StartAuthCoroutine(PostAuth("/auth/register", JsonUtility.ToJson(request)));
    }

    public void Login()
    {
        string login = usernameInput != null ? usernameInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(login))
        {
            ShowMessage("Введите никнейм или почту");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Введите пароль");
            return;
        }

        LoginRequest request = new LoginRequest
        {
            login = login,
            password = password
        };

        StartAuthCoroutine(PostAuth("/auth/login", JsonUtility.ToJson(request)));
    }

    private void StartAuthCoroutine(IEnumerator coroutine)
    {
        if (authCoroutine != null)
            StopCoroutine(authCoroutine);

        authCoroutine = StartCoroutine(coroutine);
    }

    private IEnumerator PostAuth(string endpoint, string json)
    {
        using UnityWebRequest request = new UnityWebRequest(baseUrl + endpoint, "POST");

        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        ShowMessage("Подождите...");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null
            ? request.downloadHandler.text
            : "";

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
            Debug.LogError(responseText);

            AuthResponse errorResponse = null;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    errorResponse = JsonUtility.FromJson<AuthResponse>(responseText);
                }
                catch
                {
                    errorResponse = null;
                }
            }

            if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.message))
                ShowMessage(errorResponse.message);
            else
                ShowMessage("Ошибка подключения к серверу");

            yield break;
        }

        AuthResponse response = JsonUtility.FromJson<AuthResponse>(responseText);

        if (response == null)
        {
            ShowMessage("Некорректный ответ сервера");
            yield break;
        }

        ShowMessage(response.message);

        if (response.success)
        {
            PlayerPrefs.SetInt("UserId", response.user_id);
            PlayerPrefs.SetString("Username", response.username ?? "");
            PlayerPrefs.SetString("Email", response.email ?? "");
            PlayerPrefs.SetString("FullName", response.full_name ?? "");
            PlayerPrefs.Save();

            SceneManager.LoadScene(gameSceneName);
        }
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;

        if (!string.IsNullOrWhiteSpace(message))
            Debug.Log(message);
    }
}

[System.Serializable]
public class RegisterRequest
{
    public string username;
    public string email;
    public string password;
    public string full_name;
}

[System.Serializable]
public class LoginRequest
{
    public string login;
    public string password;
}

[System.Serializable]
public class AuthResponse
{
    public bool success;
    public string message;
    public int user_id;
    public string username;
    public string email;
    public string full_name;
}