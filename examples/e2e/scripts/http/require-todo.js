const todoId = client.global.get("todoId");
if (!todoId) {
    throw new Error("Create a todo first, or press Play on 'run ./todos.http' in run-all.http. todoId comes from the create response, not an environment file.");
}
request.variables.set("todoId", todoId);
