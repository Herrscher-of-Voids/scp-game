using System;

namespace Scp.Domain
{
    public sealed class TraitInstance
    {
        private TraitParam[] _params = Array.Empty<TraitParam>();

        public ScpTrait Trait { get; set; }

        public TraitParam[] Params
        {
            get => _params;
            set
            {
                var parameters = value ?? Array.Empty<TraitParam>();
                foreach (var parameter in parameters)
                {
                    TraitParamPolicy.EnsureValid(parameter);
                }

                _params = parameters;
            }
        }

        public int Get(TraitParamKey key, int fallback = 0)
        {
            foreach (var parameter in _params)
            {
                if (parameter.Key == key)
                {
                    return parameter.Value;
                }
            }

            return fallback;
        }

        public bool Has(TraitParamKey key)
        {
            foreach (var parameter in _params)
            {
                if (parameter.Key == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
