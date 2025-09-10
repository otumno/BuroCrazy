using System.Collections.Generic;

// Атрибут [System.Serializable] очень важен. Он позволяет Unity сохранять
// этот объект и отображать его в инспекторе.
[System.Serializable]
public class DirectorDocumentLayout
{
    // В этом списке мы будем хранить состояние каждой ячейки сетки.
    // true = ошибка, false = обычный символ.
    public List<bool> gridState = new List<bool>();
}