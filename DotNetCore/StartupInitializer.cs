﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCore
{
    public class StartupInitializer : IStartupInitializer
    {
        private readonly IList<IInitializer> _initializers = new List<IInitializer>();

        public void AddInitializer(IInitializer initializer)
        {
            if (initializer is null)
            {
                return;
            }

            if (_initializers.Contains(initializer))
            {
                return;
            }

            _initializers.Add(initializer);

        }

        public async Task InitializeAsync()
        {
            foreach (var initializer in _initializers)
            {
                await initializer.InitializeAsync();
            }
        }
    }
}
