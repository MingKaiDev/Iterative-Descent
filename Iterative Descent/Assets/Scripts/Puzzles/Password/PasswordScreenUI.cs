using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PC Password Panel — mirrors PuzzleUI/PuzzleProp pattern.
/// Uses a static flag so it works regardless of whether the panel was
/// ever active before the folder puzzle was solved.
/// </summary>
public class PasswordScreenUI : MonoBehaviour
{
    public static event Action OnPC1LoggedIn;

    // Static flag — survives regardless of this GameObject's active state
    public static bool FolderPuzzleSolved = false;

    [Header("UI References")]
    public TMP_InputField passwordInputField;
    public TMP_Text statusText;
    public Button loginButton;
    public Button closeButton;

    [Header("Password Settings")]
    public string correctPassword = "C0mput3r#1";
    public float typeSpeed = 0.08f;

    [Header("Colours")]
    public Color defaultColour = Color.white;
    public Color lockedColour = new Color(0.7f, 0.1f, 0.1f, 1f);
    public Color unlockedColour = new Color(0.1f, 0.6f, 0.1f, 1f);

    private Action _onClose;

    // ── Called by ComputerScreenProp ─────────────────────────────────────────
    public void Setup(Action onClose)
    {
        _onClose = onClose;

        passwordInputField.text = "";
        passwordInputField.interactable = true;
        loginButton.interactable = true;

        loginButton.onClick.RemoveAllListeners();
        loginButton.onClick.AddListener(OnLoginClicked);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (FolderPuzzleSolved)
        {
            StartCoroutine(AutoTypePassword());
        }
        else
        {
            SetStatus("Enter password:", defaultColour);
        }
    }

    // ── Auto-type ─────────────────────────────────────────────────────────────
    IEnumerator AutoTypePassword()
    {
        passwordInputField.interactable = false;
        loginButton.interactable = false;
        SetStatus("Retrieving password...", unlockedColour);
        passwordInputField.text = "";

        foreach (char c in correctPassword)
        {
            passwordInputField.text += c;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }

        passwordInputField.interactable = true;
        loginButton.interactable = true;
        SetStatus("Password retrieved. Press Login.", unlockedColour);
    }

    // ── Login button ──────────────────────────────────────────────────────────
    void OnLoginClicked()
    {
        string entered = passwordInputField.text.Trim();

        if (entered == correctPassword)
        {
            loginButton.interactable = false;
            passwordInputField.interactable = false;
            SetStatus("Login successful!", unlockedColour);

            // Fire event — PasswordEventHandler will call ShowLinkedListPuzzle()
            // Do NOT call _onClose here; the panel swap is handled externally
            OnPC1LoggedIn?.Invoke();
        }
        else
        {
            SetStatus("ACCESS DENIED - incorrect password.", lockedColour);
            passwordInputField.text = "";
        }
    }

    // Called only by the X/close button — player manually exits without logging in
    void OnCloseClicked()
    {
        _onClose?.Invoke();
    }

    void SetStatus(string msg, Color colour)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.color = colour;
    }
}