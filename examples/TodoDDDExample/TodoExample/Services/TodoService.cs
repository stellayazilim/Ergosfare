using TodoExample.Domain;

namespace TodoExample.Services;

public class TodoService
{
    private static List<Todo> Todos { get; set; } = new();

    public void AddTodo(Todo todo)
    {
       if (Todos.Count == 0 || Todos.FirstOrDefault(x => x.Name == todo.Name) is null)
       {
           Todos.Add(todo);
       } else throw new Exception("todo already exists");
    }

    public void RemoveTodo(Todo todo)
    {
        if ( Todos.FirstOrDefault(x => x.Name == todo.Name) is { } todoToRemove)
            Todos.Remove(todoToRemove);
    }
    
    public List<Todo> GetTodos()
    {
        return Todos;
    }

    public Todo? GetTodo(string name)
    {
        return Todos.FirstOrDefault(x => x.Name == name);
    }

    public void UpdateTodo(Todo todo)
    {
        if (Todos.FirstOrDefault(x => x.Name == todo.Name) is {} todoToUpdate)
            todoToUpdate.IsCompleted = todo.IsCompleted;
    }
}