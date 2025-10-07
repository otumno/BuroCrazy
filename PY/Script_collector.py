import os
import json
import tkinter as tk
from tkinter import filedialog, END
import subprocess

# Файл настроек
SETTINGS_FILE = "settings.json"

# Загрузка настроек
def load_settings():
    if os.path.exists(SETTINGS_FILE):
        try:
            with open(SETTINGS_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
                return {
                    "last_folders": data.get("last_folders", []),
                    "last_save_dir": data.get("last_save_dir", ""),
                    "selected_extensions": data.get("selected_extensions", [".sh", ".asset"]),
                    "blacklist_folders": data.get("blacklist_folders", ["__pycache__", ".git", "node_modules"])
                }
        except Exception as e:
            print(f"Ошибка загрузки настроек: {e}")
    return {
        "last_folders": [],
        "last_save_dir": "",
        "selected_extensions": [".sh", ".asset"],
        "blacklist_folders": ["__pycache__", ".git", "node_modules"]
    }

# Сохранение настроек
def save_settings(last_folders=None, last_save_dir=None, selected_extensions=None, blacklist_folders=None):
    settings = load_settings()
    if last_folders is not None:
        settings["last_folders"] = last_folders
    if last_save_dir is not None:
        settings["last_save_dir"] = last_save_dir
    if selected_extensions is not None:
        settings["selected_extensions"] = selected_extensions
    if blacklist_folders is not None:
        settings["blacklist_folders"] = blacklist_folders
    try:
        with open(SETTINGS_FILE, 'w', encoding='utf-8') as f:
            json.dump(settings, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"Не удалось сохранить настройки: {e}")

# Загружаем настройки
settings = load_settings()
last_folders = settings["last_folders"]
last_save_dir = settings["last_save_dir"]
saved_extensions = settings["selected_extensions"]
saved_blacklist = settings["blacklist_folders"]

# Глобальные переменные
selected_folders = last_folders.copy() if last_folders else []
blacklisted_subfolders = saved_blacklist.copy()  # Только имена папок (не полные пути)

# === GUI ===
root = tk.Tk()
root.title("Сборщик файлов")
root.geometry("650x650")
root.resizable(False, False)

# Центрируем окно
screen_width = root.winfo_screenwidth()
screen_height = root.winfo_screenheight()
x = (screen_width - 650) // 2
y = (screen_height - 650) // 2
root.geometry(f"650x650+{x}+{y}")

# --- Заголовок ---
title_label = tk.Label(root, text="Сборщик файлов", font=("Arial", 16))
title_label.pack(pady=10)

# --- Описание ---
desc = tk.Label(root, text="Выберите папки, типы файлов и исключите ненужные подпапки", font=("Arial", 10))
desc.pack(pady=5)


# --- Выбор корневых папок ---
folder_frame = tk.LabelFrame(root, text="Корневые папки для поиска", padx=10, pady=10)
folder_frame.pack(pady=10, fill="x", padx=20)

folders_listbox = tk.Listbox(folder_frame, height=5)
folders_scrollbar = tk.Scrollbar(folder_frame, orient="vertical", command=folders_listbox.yview)
folders_listbox.config(yscrollcommand=folders_scrollbar.set)
folders_listbox.pack(side="left", fill="x", expand=True)
folders_scrollbar.pack(side="right", fill="y")

btn_frame1 = tk.Frame(folder_frame)
btn_frame1.pack(pady=5)

add_folder_btn = tk.Button(btn_frame1, text="Добавить папку", width=15, command=lambda: add_to_list(folders_listbox, selected_folders))
add_folder_btn.pack(side="left", padx=2)

remove_folder_btn = tk.Button(btn_frame1, text="Удалить", width=10, command=lambda: remove_from_list(folders_listbox, selected_folders))
remove_folder_btn.pack(side="left", padx=2)

# Добавляем старые папки
for folder in selected_folders:
    folders_listbox.insert(END, folder)


# --- Чёрный список подпапок ---
blacklist_frame = tk.LabelFrame(root, text="Чёрный список подпапок (игнорировать)", padx=10, pady=10)
blacklist_frame.pack(pady=10, fill="x", padx=20)

blacklist_listbox = tk.Listbox(blacklist_frame, height=5)
blacklist_scrollbar = tk.Scrollbar(blacklist_frame, orient="vertical", command=blacklist_listbox.yview)
blacklist_listbox.config(yscrollcommand=blacklist_scrollbar.set)
blacklist_listbox.pack(side="left", fill="x", expand=True)
blacklist_scrollbar.pack(side="right", fill="y")

btn_frame2 = tk.Frame(blacklist_frame)
btn_frame2.pack(pady=5)

add_black_btn = tk.Button(btn_frame2, text="Добавить папку", width=15, command=lambda: add_to_list(blacklist_listbox, blacklisted_subfolders))
add_black_btn.pack(side="left", padx=2)

remove_black_btn = tk.Button(btn_frame2, text="Удалить", width=10, command=lambda: remove_from_list(blacklist_listbox, blacklisted_subfolders))
remove_black_btn.pack(side="left", padx=2)

# Подсказка
hint = tk.Label(blacklist_frame, text="Введите имя папки (например: Fonts, Temp, node_modules)", font=("Arial", 8), fg="gray")
hint.pack()

# Добавляем старый чёрный список
for name in blacklisted_subfolders:
    blacklist_listbox.insert(END, name)


# --- Выбор типов файлов ---
ext_frame = tk.Frame(root)
ext_frame.pack(pady=15)

tk.Label(ext_frame, text="Типы файлов для сбора:", font=("Arial", 10)).pack(anchor="w", padx=10)

extension_vars = {}
for ext in [".sh", ".asset", ".cs"]:
    var = tk.BooleanVar(value=ext in saved_extensions)
    extension_vars[ext] = var
    cb = tk.Checkbutton(ext_frame, text=ext, variable=var, font=("Arial", 10))
    cb.pack(anchor="w", padx=20)


# === Универсальные функции для списков ===
def add_to_list(listbox, data_list):
    folder = filedialog.askdirectory(title="Выберите папку или введите имя")
    if folder:
        # Если пользователь выбрал папку — берём только её имя (basename)
        folder_name = os.path.basename(folder)
        if folder_name not in data_list:
            data_list.append(folder_name)
            listbox.insert(END, folder_name)

def remove_from_list(listbox, data_list):
    selected_idx = listbox.curselection()
    if selected_idx:
        index = selected_idx[0]
        data_list.pop(index)
        listbox.delete(index)


# === Сборка файлов ===
def collect_files():
    global selected_folders, blacklisted_subfolders, last_save_dir

    # Получаем выбранные расширения
    extensions = [ext for ext, var in extension_vars.items() if var.get()]
    if not extensions:
        return

    # Сохраняем всё
    save_settings(
        last_folders=selected_folders,
        last_save_dir=last_save_dir,
        selected_extensions=extensions,
        blacklist_folders=blacklisted_subfolders
    )

    # Проверяем, есть ли папки
    if not selected_folders:
        return

    # Диалог сохранения
    initial_dir = last_save_dir or (os.path.dirname(selected_folders[0]) if selected_folders else "")
    output_file = filedialog.asksaveasfilename(
        defaultextension=".txt",
        filetypes=[("Text files", "*.txt"), ("All files", "*.*")],
        title="Сохранить как",
        initialdir=initial_dir,
        initialfile="collected_files.txt"
    )
    if not output_file:
        return

    # Обновляем last_save_dir
    last_save_dir = os.path.dirname(output_file)
    save_settings(last_save_dir=last_save_dir)

    try:
        with open(output_file, 'w', encoding='utf-8') as out_f:
            found_any = False
            for base_folder in selected_folders:
                if not os.path.exists(base_folder):
                    out_f.write(f"---\n[Папка не найдена: {base_folder}]\n---\n\n")
                    continue

                for root, dirs, files in os.walk(base_folder):
                    # --- Фильтрация: удаляем из обхода папки из чёрного списка ---
                    dirs[:] = [d for d in dirs if d not in blacklisted_subfolders]

                    for file in files:
                        if any(file.endswith(ext) for ext in extensions):
                            file_path = os.path.join(root, file)
                            rel_path_from_base = os.path.relpath(file_path, base_folder)
                            display_path = f"{os.path.basename(base_folder)}/{rel_path_from_base}"

                            out_f.write('---\n')
                            out_f.write(f'{display_path}\n')
                            out_f.write('---\n')

                            try:
                                with open(file_path, 'r', encoding='utf-8') as script_f:
                                    content = script_f.read()
                                    out_f.write(content)
                                    if not content.endswith('\n'):
                                        out_f.write('\n')
                                    out_f.write('\n---\n')
                                found_any = True
                            except UnicodeDecodeError:
                                out_f.write(f"[Ошибка: бинарный файл]\n\n---\n")
                            except Exception as e:
                                out_f.write(f"[Ошибка: {str(e)}]\n\n---\n")

            if not found_any:
                out_f.write("---\nФайлы не найдены\n---\n")

        # Открываем проводник с выделением
        subprocess.run(['explorer', '/select,', os.path.normpath(output_file)], shell=True)

    except Exception as e:
        pass  # Молча игнорируем


# --- Кнопка сборки ---
collect_btn = tk.Button(
    root, text="Собрать файлы",
    font=("Arial", 12), width=30, height=2,
    command=collect_files
)
collect_btn.pack(pady=20)

# --- Подпись ---
footer = tk.Label(root, text="by Python + Tkinter", font=("Arial", 8), fg="gray")
footer.pack(side="bottom", pady=10)

# --- Запуск ---
root.mainloop()