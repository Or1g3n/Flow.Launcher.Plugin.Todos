    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Controls;
    using Flow.Launcher.Plugin;

    namespace Flow.Launcher.Plugin.Todos
    {
        public class Main : IPlugin, ISettingProvider, IContextMenu
        {
            private IContextMenu contextMenu;
            private static Todos _todos;
            private static Todo _todo_to_edit = null;
            internal Settings _setting;

              public void Init(PluginInitContext context)
            {
                _setting = context.API.LoadSettingJsonStorage<Settings>();
                _todos = new Todos(context, _setting);
                contextMenu = new ContextMenu(context, _setting);
            }

            public Control CreateSettingPanel()
            {
                return new FilePathSetting(_setting);
            }

            public List<Result> LoadContextMenus(Result selectedResult)
            {
                return contextMenu.LoadContextMenus(selectedResult);
            }

            public List<Result> Query(Query query)
            {
                _todos.Reload();
                _todos.ActionKeyword = query.ActionKeyword;

                var help = new Help(_todos.Context, query);

                if (query.FirstSearch.Equals("-"))
                {
                    _todo_to_edit = null; // enforce reset of _todo_to_edit if user exited edit process early
                    return help.Show;
                }

                if (!query.FirstSearch.StartsWith("-"))
                {
                    return Search(query.Search, t => !t.Completed);
                }

                if (!Enum.TryParse(query.FirstSearch.TrimStart('-'), true, out TodoCommand op))
                {
                    return Search(query.Search, t => !t.Completed);
                }

                switch (op)
                {
                    case TodoCommand.H:
                        return help.GetHelpResults(query.SecondToEndSearch);
                    case TodoCommand.U:
                        return HandleUncheck(query);
                    case TodoCommand.C:
                        return HandleComplete(query);
                    case TodoCommand.P:
                        return HandlePin(query);
                    case TodoCommand.R:
                        return HandleRemove(query);
                    case TodoCommand.S:
                        return HandleSort(query);
                    case TodoCommand.A:
                        return new List<Result> { AddResult(query.SecondToEndSearch) };
                    case TodoCommand.E:
                        if (_todo_to_edit == null)
                        {
                            return HandleEdit(query);
                        }
                        else
                        {
                            return new List<Result> { EditResult(query.SecondToEndSearch) };
                        }
                    case TodoCommand.L:
                        return Search(query.SecondToEndSearch);
                    case TodoCommand.Rl:
                        return new List<Result> {
                            new Result {
                                Title = "Reload todos from data file?",
                                SubTitle = "Click to reload",
                                IcoPath = _todos.GetFilePath(),
                                Action = c => {
                                    _todos.Reload();
                                    _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                    return false;
                                }
                            }
                        };
                    default:
                        return Search(query.Search, t => !t.Completed);
                }
            }

            private List<Result> Search(string search, Func<Todo, bool> conditions = null)
            {
                var results = _todos.Find(t =>
                    t.Content.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    && (conditions?.Invoke(t) ?? true));

                if (!results.Any() && !string.IsNullOrEmpty(search))
                {
                    results.Insert(0, AddResult(search));
                }
                return results;
            }

            private Result AddResult(string content)
            {
                return new Result
                {
                    Title = $"Add new item \"{content}\"",
                    SubTitle = "",
                    IcoPath = _todos.GetFilePath(),
                    Action = c =>
                    {
                        _todos.Add(new Todo
                        {
                            Content = content,
                            Completed = false,
                            Pinned = false,
                            CreatedTime = DateTime.Now
                        });
                        return false;
                    }
                };
            }

            private Result EditResult(string content)
            {
                return new Result
                {
                    Title = $"Enter new title: \"{content}\"",
                    SubTitle = $"Current title: {_todo_to_edit.Content}. Press Enter when done.",
                    IcoPath = _todos.GetFilePath(),
                    Action = c =>
                    {
                        _todos.Edit(_todo_to_edit, content);
                        _todo_to_edit = null; // Reset to null after editing is done
                        return false;
                    }
                };
            }

            private List<Result> HandleUncheck(Query query)
            {
                if (query.SecondSearch.Equals("--all", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Mark all todos as not done?",
                            SubTitle = "Click to mark all todos as not done",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.UncheckAll();
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }
                else if (query.SecondSearch.Contains("-"))
                {
                    // Show all secondary commands
                    var secondaryOptions = new List<Result>();
                    var help = new Help(_todos.Context, query);

                    foreach (var result in help.Show)
                    {
                        if (result.Title.StartsWith($"{query.ActionKeyword} -u --", StringComparison.OrdinalIgnoreCase))
                        {
                            secondaryOptions.Add(result);
                        }
                    }
                    return secondaryOptions;
                }

                return _todos.Find(
                    t => t.Content.IndexOf(query.SecondToEndSearch, StringComparison.OrdinalIgnoreCase) >= 0 && t.Completed,
                    t => "Click to mark todo as not done",
                    (c, t) => {
                        _todos.Uncheck(t);
                        _todos.Context.API.ChangeQuery($"{query.ActionKeyword} -u ", true);
                        return false;
                    });
            }

            private List<Result> HandleComplete(Query query)
            {
                if (query.SecondSearch.Equals("--all", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Mark all todos as done?",
                            SubTitle = "Click to mark all todos as done",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.CompleteAll();
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }
                else if (query.SecondSearch.Contains("-"))
                {
                    // Show all secondary commands 
                    var secondaryOptions = new List<Result>();
                    var help = new Help(_todos.Context, query);

                    foreach (var result in help.Show)
                    {
                        if (result.Title.StartsWith($"{query.ActionKeyword} -c --", StringComparison.OrdinalIgnoreCase))
                        {
                            secondaryOptions.Add(result);
                        }
                    }
                    return secondaryOptions;
                }

                return _todos.Find(
                    t => t.Content.IndexOf(query.SecondToEndSearch, StringComparison.OrdinalIgnoreCase) >= 0 && !t.Completed,
                    t => "Click to mark todo as done",
                    (c, t) => {
                        _todos.Complete(t);
                        _todos.Context.API.ChangeQuery($"{query.ActionKeyword} -c ", true);
                        return false;
                    });
            }

            private List<Result> HandlePin(Query query)
            {
                if (query.SecondSearch.Equals("--u", StringComparison.OrdinalIgnoreCase))
                {
                    return _todos.Find(
                        t => t.Content.IndexOf(query.ThirdSearch, StringComparison.OrdinalIgnoreCase) >= 0 && !t.Completed && t.Pinned,
                        t => "Click to unpin todo",
                        (c, t) => {
                            _todos.UnPin(t);
                            _todos.Context.API.ChangeQuery($"{query.ActionKeyword} -p --u ", true);
                            return false;
                        });
                }
                else if (query.SecondSearch.Contains("-"))
                {
                    // Show all secondary commands 
                    var secondaryOptions = new List<Result>();
                    var help = new Help(_todos.Context, query);

                    foreach (var result in help.Show)
                    {
                        if (result.Title.StartsWith($"{query.ActionKeyword} -p --", StringComparison.OrdinalIgnoreCase))
                        {
                            secondaryOptions.Add(result);
                        }
                    }
                    return secondaryOptions;
                }

                return _todos.Find(
                    t => t.Content.IndexOf(query.SecondToEndSearch, StringComparison.OrdinalIgnoreCase) >= 0 && !t.Completed && !t.Pinned,
                    t => "Click to pin todo",
                    (c, t) => {
                        _todos.Pin(t);
                        _todos.Context.API.ChangeQuery($"{query.ActionKeyword} -p ", true);
                        return false;
                    });
            }

            private List<Result> HandleRemove(Query query)
            {
                if (query.SecondSearch.Equals("--all", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Remove all todos?",
                            SubTitle = "Click to remove all todos",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.RemoveAll();
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }
                else if (query.SecondSearch.Equals("--done", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Remove all completed todos?",
                            SubTitle = "Click to remove all completed todos",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.RemoveAllCompletedTodos();
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }
                else if (query.SecondSearch.Contains("-"))
                {
                    // Show all secondary commands 
                    var secondaryOptions = new List<Result>();
                    var help = new Help(_todos.Context, query);

                    foreach (var result in help.Show)
                    {
                        if (result.Title.StartsWith($"{query.ActionKeyword} -r --", StringComparison.OrdinalIgnoreCase))
                        {
                            secondaryOptions.Add(result);
                        }
                    }
                    return secondaryOptions;
                }

                return _todos.Find(
                    t => t.Content.IndexOf(query.SecondToEndSearch, StringComparison.OrdinalIgnoreCase) >= 0,
                    t => "Click to remove todo",
                    (c, t) => {
                        _todos.Remove(t);
                        _todos.Context.API.ChangeQuery($"{query.ActionKeyword} -r ", true);
                        return false;
                    });
            }

            private List<Result> HandleEdit(Query query)
            {
                _todo_to_edit = null; // Ensure _todo_to_edit is reset for the next operation
                return _todos.Find(
                    t => t.Content.IndexOf(query.SecondToEndSearch, StringComparison.OrdinalIgnoreCase) >= 0 && !t.Completed,
                    t => "Click to edit todo",
                    (c, t) => {
                        _todo_to_edit = t;
                        _todos.Context.API.ChangeQuery($"{query.ActionKeyword} -e {t.Content}", true);
                        return false;
                    });
            }

            private List<Result> HandleSort(Query query)
            {
                if (query.SecondSearch.Equals("--aa", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Sort todos alphabetical ascending?",
                            SubTitle = "Click to sort todos alphabetical ascending",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.SetSortOption(SortOption.AlphabeticalAscending);
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }
                else if (query.SecondSearch.Equals("--ad", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Sort todos alphabetical descending?",
                            SubTitle = "Click to sort todos alphabetical descending",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.SetSortOption(SortOption.AlphabeticalDescending);
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    }; ;
                }
                else if (query.SecondSearch.Equals("--ta", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Sort todos time ascending?",
                            SubTitle = "Click to sort todos time ascending",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.SetSortOption(SortOption.TimeAscending);
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }
                else if (query.SecondSearch.Equals("--td", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<Result> {
                        new Result {
                            Title = "Sort todos time descending?",
                            SubTitle = "Click to sort todos time descending",
                            IcoPath = _todos.GetFilePath(),
                            Action = c => {
                                _todos.SetSortOption(SortOption.TimeDescending);
                                _todos.Context.API.ChangeQuery($"{query.ActionKeyword} ", true);
                                return false;
                            }
                        }
                    };
                }

                // If invalid sort option get sorting options from Help.Show
                var sortOptions = new List<Result>();
                var help = new Help(_todos.Context, query);

                foreach (var result in help.Show)
                {
                    if (result.Title.StartsWith($"{query.ActionKeyword} -s", StringComparison.OrdinalIgnoreCase))
                    {
                        sortOptions.Add(result);
                    }
                }
                return sortOptions;
            }
        }
    }