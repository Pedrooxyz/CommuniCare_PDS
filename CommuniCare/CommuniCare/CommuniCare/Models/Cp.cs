﻿using System;
using System.Collections.Generic;

namespace CommuniCare.Models;

public partial class Cp
{
    public string CPostal { get; set; }

    public string? Localidade { get; set; }

    public virtual ICollection<Morada> Morada { get; set; } = new List<Morada>();
}
