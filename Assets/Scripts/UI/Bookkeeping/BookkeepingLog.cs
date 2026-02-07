using TMPro;
using UnityEngine;

namespace UI.Bookkeeping
{
    public class BookkeepingLog : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;

        public void Init(string text)
        {
            _text.text = text;
        }
    }
}