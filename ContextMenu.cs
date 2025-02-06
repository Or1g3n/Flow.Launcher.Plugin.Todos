using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using Flow.Launcher.Plugin.Todos;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Todos
{
    internal class ContextMenu : IContextMenu
    {
        private PluginInitContext Context { get; set; }
        private Settings Settings { get; set; }

        private static Todos _todos;

        public ContextMenu(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
            _todos = new Todos(context, settings);
        }

        public string GetFilePath(string icon = "")
        {
            return Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, string.IsNullOrEmpty(icon) ? @"ico\app.png" : icon);
        }

        public void Alert(string title, string content)
        {
            Context.API.ShowMsg(title, content, GetFilePath());
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();

            // Check if the selected result's ContextData is a Todo item
            if (selectedResult.ContextData is Todo todo)
            {
                // Add options for the Todo item
                contextMenus.Add(CreateCompleteMenu(todo));
                //contextMenus.Add(CreateEditMenu(todo));
                contextMenus.Add(CreatePinMenu(todo));
            }

            return contextMenus;
        }

        private Result CreateCompleteMenu(Todo todo)
        {
            return new Result
            {
                Title = todo.Completed ? "Mark as Uncompleted" : "Mark as Completed",
                SubTitle = todo.Completed ? "Click to mark this todo as not completed" : "Click to mark this todo as completed",
                IcoPath = GetFilePath(@"ico\done.png"),
                Action = _ =>
                {
                    if (todo.Completed)
                    {
                        _todos.Uncheck(todo);
                    }
                    else
                    {
                        _todos.Complete(todo); 
                    }

                    //Context.API.ChangeQuery($"td ", true);
                    return false;  // Return true to indicate that the action was performed
                }
            };
        }

        private Result CreateEditMenu(Todo todo) // TODO THIS ISNT THE RIGHT SETUP
        {
            return new Result
            {
                Title = "Edit Todo",
                SubTitle = "Click to edit this todo item",
                IcoPath = GetFilePath(),
                Action = _ =>
                {
                    // Implement the logic for editing the todo
                    //Context.API.ChangeQuery($"-e {todo.Content}", true);
                    return false;
                }
            };
        }

        private Result CreatePinMenu(Todo todo)
        {
            return new Result
            {
                Title = todo.Pinned ? "Unpin Todo" : "Pin Todo",
                SubTitle = todo.Pinned ? "Click to unpin this todo" : "Click to pin this todo",
                IcoPath = GetFilePath(),
                Action = _ =>
                {
                    // Toggle pinning the todo
                    if (todo.Pinned)
                    {
                        _todos.UnPin(todo);
                    }
                    else
                    {
                        _todos.Pin(todo);
                    }

                    //Context.API.ChangeQuery($"td ", true);
                    return false;
                }
            };
        }
    }
}
