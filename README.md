# Commitiownie

Do mastera nie commitujemy. Robimy nowego brancha dla danego issue. 

Jak skończymy pracę to robimy merge requesta:
https://docs.gitlab.com/ee/gitlab-basics/add-merge-request.html

Zaznaczymy w merge request, żeby zamykało issue po zamknięciu merge requesta!

# Pierwsze uruchomienie

1. Klonujemy repo
3. Installujemy npm jak nie mamy
3. Ustawiamy sobie zmienną środowiskową na:
```
ASPNETCORE_ENVIRONMENT=Development
```

4. Uruchamiamy (będą w src/AutoAllegro):
```
npm install gulp -g
npm install
dotnet restore
dotnet run
```
5. Jeśli jest to pierwsze uruchomienie albo ty/ktoś zmienił bazę to robimy:
```
dotnet ef database update
```
Więcej o migracjach bazy po aktualizowaniu modelu można poczytać na necie.

6. Na localhost:5000 mamy stronke
7. Domyślny user: admin@allegro.pl:admin

# Pomoce
Przykładowy projekt w którym możemy zobaczyć jak zrobić wiele rzeczy:
https://github.com/aspnet/MusicStore/tree/rel/1.1.0