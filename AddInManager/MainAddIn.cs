﻿using ExcelDna.Integration;

namespace AddInManager;

public class MainAddIn : IExcelAddIn
{
    public void AutoOpen()
    {
        controller = new Controller();
        
    }

    public void AutoClose()
    {
    }

    internal static Controller? GetController()
    {
        return controller;
    }

    private static Controller? controller;
}

