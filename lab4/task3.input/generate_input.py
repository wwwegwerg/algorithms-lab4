import random

# количество слов
n = 1_000_000  # измените при необходимости

# базовый набор частотных английских слов
word_pool = """
time year people way day man thing woman life child world school state family student group country problem hand
part place case week company system program question work government number night point home water room mother area
money story fact month lot right study book eye job word business issue side kind head house service friend father
power hour game line end member law car city community name president team minute idea kid body information back
parent face others level office door health person art war history party result change morning reason research girl guy
moment air teacher force education foot boy age policy everything process music market sense activity road
""".strip().split()

# расширяем список — добавляем формы и служебные слова
extras = ["the", "and", "to", "of", "in", "for", "on", "with", "from", "by", "about"]
for w in list(word_pool):
    extras.append(w + "s")
word_pool = list(dict.fromkeys(word_pool + extras))

# генерация n случайных слов
text = " ".join(random.choice(word_pool) for _ in range(n))

# запись в файл
with open("generated_text.txt", "w", encoding="utf-8") as f:
    f.write(text + "\n")

print(f"Сгенерировано {n} слов и сохранено в 'generated_text.txt'")
