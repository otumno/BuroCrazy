# Чтение проекта через Rider MCP

Нужен открытый BuroCrazy в Rider и включённый MCP server на `http://127.0.0.1:64342/stream`.
Клиент использует стандартную библиотеку Python; токенов и сторонних пакетов нет.

```powershell
python -B Tools/Rider/read.py list
python -B Tools/Rider/read.py schema get_file_problems
python -B Tools/Rider/read.py read Assets/Scripts/Managers/HiringManager.cs 400 40
python -B Tools/Rider/read.py problems Assets/Scripts/Managers/HiringManager.cs
python -B Tools/Rider/read.py search 'StartShift()' 'Assets/Scripts/Characters/Controllers/**'
```

Разрешены только чтение, поиск и диагностика. Выполнение терминальных команд, редактирование и рефакторинги через этот клиент недоступны. После внешнего изменения файла IDE может некоторое время возвращать прежнее содержимое из кеша; свежие правки проверять также на диске и компиляцией Unity.

В песочнице может потребоваться разрешённый доступ к локальному серверу вне песочницы. Отказ проверки разрешений обходить нельзя.
