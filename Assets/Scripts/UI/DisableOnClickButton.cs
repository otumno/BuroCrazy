using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class DisableOnClickButton : MonoBehaviour
    {
        [SerializeField]
        private GameObject _target;

        [SerializeField]
        private Button _button;

        private void Start()
        {
            _button.onClick.AddListener(() => _target.SetActive(false));
        }
    }
}